# Plan: Report + Moderation Tooling

**Status:** planned (not yet implemented)
**Goal:** lightweight community reporting + a simple moderator review interface.
Scope intentionally small: a report button on forum posts and on guides, plus a
dashboard where moderators can review reports and take action.

Builds on the automated protections already in place (slur blocklist, length
caps, rate limiting, markdown sanitizer). Human moderation is the real backstop.

---

## 1. Data model

**`Report` entity** (new table):

| Field | Type | Notes |
|-------|------|-------|
| Id | int | PK |
| ReporterId | string? | FK → AspNetUsers (require auth to report) |
| TargetType | enum `ReportTargetType` { Post, Guide } | what was reported |
| TargetId | int | id of the Post or Guide |
| Reason | enum `ReportReason` { Spam, Harassment, HateSpeech, Nsfw, OffTopic, Other } | |
| Details | string? | optional free text, length-capped + run through ContentModerationService |
| Status | enum `ReportStatus` { Open, Resolved, Dismissed } | default Open |
| CreatedAt | DateTimeOffset | |
| ResolvedById | string? | moderator who actioned it |
| ResolvedAt | DateTimeOffset? | |

Indexes: `(Status, CreatedAt)`, `(TargetType, TargetId)`.
One EF migration.

## 2. Moderator role

- Use ASP.NET Identity **roles**; add a `"Moderator"` role (seed the role on
  startup, idempotent).
- Grant to the founder account manually at first (SQL insert into
  `AspNetUserRoles`, or a tiny one-off bootstrap that promotes a configured
  email). Document the SQL in the dashboard page header comment.
- Protect all moderation endpoints with `[Authorize(Roles = "Moderator")]`.

## 3. Reporting flow (users)

- **Report button** on:
  - each forum **post** (in the post meta area, signed-in users only), and
  - the **guide** view header (next to / instead of the owner actions for
    non-owners).
- Clicking opens a small inline form (reason dropdown + optional details) that
  POSTs to a `ReportModel` handler. Keep it a standard form post (htmx optional
  later); no modal library.
- Server side (`ReportService`):
  - require authentication;
  - validate target exists;
  - **dedupe**: ignore if this user already has an Open report on this target;
  - apply the existing `"post"` rate-limit policy to prevent report spam;
  - run `Details` through `ContentModerationService`.
  - store and show a "Thanks, a moderator will take a look" confirmation.

## 4. Moderator dashboard (`/Moderation`)

`[Authorize(Roles = "Moderator")]`, linked in the nav only for moderators.

- **Queue view:** open reports, newest first, **grouped by target** so multiple
  reports on the same content collapse into one row with a count.
- Each row shows: content excerpt, author, board/champion context, a link to the
  live item, reporter(s), reason(s), and time.
- **Actions** (POST handlers, each records ResolvedBy/At and sets Status):
  - **Dismiss** — no action, mark Dismissed.
  - **Delete content** — remove the post or guide (reuse existing delete paths).
  - **Lock thread** — for post reports, set `ForumThread.IsLocked = true`.
  - **Ban user** (optional, phase 2) — flag/lockout the author via Identity
    (`SetLockoutEndDateAsync` far future) so they can't post.
- Simple filter tabs: Open / Resolved / Dismissed.

## 5. Nav + UX

- Show a **"Moderation"** nav link (with an open-report count badge) only to
  users in the Moderator role.
- Keep owner self-delete on guides/threads as-is.

## 6. Explicitly out of scope (keep it lightweight)

- Email/push notifications to moderators.
- Full audit log / moderation history beyond ResolvedBy/At.
- Automated escalation, report weighting, or trust levels.
- Appeals workflow.
- AI moderation API (tracked separately as a possible future layer).

## 7. Build order (small, shippable steps)

1. `Report` entity + enums + DbContext config + migration.
2. Seed `Moderator` role; document how to grant it.
3. `ReportService` (create/dedupe/validate) + report button/form partial on
   posts and guides.
4. `/Moderation` dashboard: grouped queue + Dismiss/Delete/Lock actions.
5. Moderator-only nav link with open count.
6. (Phase 2) Ban action; Resolved/Dismissed history tabs.

## 8. Verification checklist

- Non-auth user can't report; auth user can; duplicate open report is ignored.
- Report `Details` is length-capped and blocklist-filtered.
- Non-moderator gets 403 on `/Moderation`; moderator sees the queue.
- Dismiss / Delete / Lock each update status + take the real action.
- Deleting reported content resolves its reports.
