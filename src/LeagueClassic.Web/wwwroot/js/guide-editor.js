// League Classic — guide editor. Dependency-free; serializes UI state into
// hidden inputs that the Razor page binds on submit.
(function () {
    'use strict';

    // ---- Summoner spells (pick up to 2) ----
    const spellPalette = document.getElementById('spell-palette');
    const spellOne = document.getElementById('spell-one');
    const spellTwo = document.getElementById('spell-two');
    let selectedSpells = [];

    function renderSpells() {
        spellPalette.querySelectorAll('.spell-opt').forEach(btn => {
            const id = Number(btn.dataset.id);
            btn.classList.toggle('selected', selectedSpells.includes(id));
        });
        spellOne.value = selectedSpells[0] ?? '';
        spellTwo.value = selectedSpells[1] ?? '';
    }
    spellPalette.addEventListener('click', e => {
        const btn = e.target.closest('.spell-opt');
        if (!btn) return;
        const id = Number(btn.dataset.id);
        const i = selectedSpells.indexOf(id);
        if (i >= 0) selectedSpells.splice(i, 1);
        else if (selectedSpells.length < 2) selectedSpells.push(id);
        renderSpells();
    });

    // ---- Ability leveling grid (4 abilities x 18 levels) ----
    const grid = document.getElementById('skill-grid');
    const skillInput = document.getElementById('skill-order');
    const ABILITIES = ['Q', 'W', 'E', 'R'];
    const LEVELS = 18;
    // Per-champion ability names/icons injected by the page.
    let abilityInfo = {};
    try {
        abilityInfo = JSON.parse(document.getElementById('champ-abilities').textContent);
    } catch (e) { /* fall back to bare Q/W/E/R labels */ }
    const R_LEVELS = new Set([6, 11, 16]); // 1-based levels R is allowed
    const levelState = {}; // level(1..18) -> ability letter

    function buildGrid() {
        const table = document.createElement('table');
        table.className = 'skill-table';

        // header row: level numbers
        const thead = document.createElement('thead');
        const hr = document.createElement('tr');
        hr.appendChild(document.createElement('th')); // corner
        for (let lvl = 1; lvl <= LEVELS; lvl++) {
            const th = document.createElement('th');
            th.textContent = lvl;
            hr.appendChild(th);
        }
        thead.appendChild(hr);
        table.appendChild(thead);

        const tbody = document.createElement('tbody');
        ABILITIES.forEach(ab => {
            const tr = document.createElement('tr');
            const label = document.createElement('th');
            label.className = 'ab-label ab-' + ab;
            const info = abilityInfo[ab];
            if (info) {
                label.title = info.name;
                if (info.icon) {
                    const img = document.createElement('img');
                    img.src = '/' + info.icon;
                    img.alt = info.name;
                    label.appendChild(img);
                }
                const key = document.createElement('span');
                key.className = 'slot-key slot-' + ab;
                key.textContent = ab;
                label.appendChild(key);
            } else {
                label.textContent = ab;
            }
            tr.appendChild(label);
            for (let lvl = 1; lvl <= LEVELS; lvl++) {
                const td = document.createElement('td');
                const cell = document.createElement('button');
                cell.type = 'button';
                cell.className = 'skill-cell';
                cell.dataset.ability = ab;
                cell.dataset.level = lvl;
                if (ab === 'R' && !R_LEVELS.has(lvl)) {
                    cell.disabled = true;
                    cell.classList.add('locked');
                }
                td.appendChild(cell);
                tr.appendChild(td);
            }
            tbody.appendChild(tr);
        });
        table.appendChild(tbody);
        grid.appendChild(table);
    }

    function renderGrid() {
        grid.querySelectorAll('.skill-cell').forEach(cell => {
            const lvl = Number(cell.dataset.level);
            cell.classList.toggle('on', levelState[lvl] === cell.dataset.ability);
        });
        // serialize: level 1..maxFilled, '-' for gaps
        let max = 0;
        for (let lvl = 1; lvl <= LEVELS; lvl++) if (levelState[lvl]) max = lvl;
        const slots = [];
        for (let lvl = 1; lvl <= max; lvl++) slots.push(levelState[lvl] || '-');
        skillInput.value = slots.join(',');
    }

    grid.addEventListener('click', e => {
        const cell = e.target.closest('.skill-cell');
        if (!cell || cell.disabled) return;
        const lvl = Number(cell.dataset.level);
        const ab = cell.dataset.ability;
        levelState[lvl] = (levelState[lvl] === ab) ? undefined : ab;
        renderGrid();
    });

    // ---- Item build order ----
    const strip = document.getElementById('build-strip');
    const buildInput = document.getElementById('build-order');
    const itemPalette = document.querySelector('.item-palette');
    let build = []; // [{id, name, icon}]

    function renderBuild() {
        strip.innerHTML = '';
        if (build.length === 0) {
            const span = document.createElement('span');
            span.className = 'build-empty';
            span.textContent = 'Click items below to add them to the build in order.';
            strip.appendChild(span);
        } else {
            build.forEach((it, idx) => {
                const chip = document.createElement('button');
                chip.type = 'button';
                chip.className = 'build-chip';
                chip.title = 'Remove ' + it.name;
                chip.dataset.idx = idx;
                chip.innerHTML =
                    '<span class="ord">' + (idx + 1) + '</span>' +
                    '<img src="/' + it.icon + '" alt="' + it.name + '">' +
                    '<span class="x">×</span>';
                strip.appendChild(chip);
            });
        }
        buildInput.value = build.map(b => b.id).join(',');
    }

    itemPalette.addEventListener('click', e => {
        const btn = e.target.closest('.item-opt');
        if (!btn) return;
        build.push({ id: Number(btn.dataset.id), name: btn.dataset.name, icon: btn.dataset.icon });
        renderBuild();
    });
    strip.addEventListener('click', e => {
        const chip = e.target.closest('.build-chip');
        if (!chip) return;
        build.splice(Number(chip.dataset.idx), 1);
        renderBuild();
    });

    // pre-populate from an existing guide (edit mode)
    try {
        const init = JSON.parse(document.getElementById('initial-state').textContent);
        if (init) {
            if (Array.isArray(init.spells)) selectedSpells = init.spells.slice(0, 2).map(Number);
            if (Array.isArray(init.build))
                build = init.build.map(b => ({ id: Number(b.id), name: b.name, icon: b.icon }));
            if (typeof init.skill === 'string' && init.skill) {
                init.skill.split(',').forEach((slot, i) => {
                    const s = slot.trim().toUpperCase();
                    if (['Q', 'W', 'E', 'R'].includes(s)) levelState[i + 1] = s;
                });
            }
        }
    } catch (e) { /* create mode — nothing to restore */ }

    // ---- Hover description panel (items + spells) ----
    const hoverPanel = document.createElement('div');
    hoverPanel.className = 'hover-desc';
    hoverPanel.style.display = 'none';
    document.body.appendChild(hoverPanel);

    function esc(s) { const d = document.createElement('div'); d.textContent = s || ''; return d.innerHTML; }

    function positionHover(e) {
        const pad = 14, w = hoverPanel.offsetWidth, h = hoverPanel.offsetHeight;
        let x = e.clientX + pad, y = e.clientY + pad;
        if (x + w > window.innerWidth) x = e.clientX - w - pad;
        if (y + h > window.innerHeight) y = Math.max(4, window.innerHeight - h - pad);
        hoverPanel.style.left = x + 'px';
        hoverPanel.style.top = y + 'px';
    }
    function showHover(el, e) {
        const name = el.dataset.name, desc = el.dataset.desc;
        if (!name) return;
        hoverPanel.innerHTML = '<div class="hd-name">' + esc(name) + '</div>' +
            (desc && desc.trim()
                ? '<div class="hd-desc">' + esc(desc).replace(/\n/g, '<br>') + '</div>'
                : '<div class="hd-desc hd-none">No description available.</div>');
        hoverPanel.style.display = 'block';
        positionHover(e);
    }
    [document.querySelector('.item-palette'), document.getElementById('spell-palette')].forEach(c => {
        if (!c) return;
        c.addEventListener('mouseover', e => {
            const el = e.target.closest('.item-opt, .spell-opt');
            if (el) showHover(el, e);
        });
        c.addEventListener('mousemove', e => {
            if (hoverPanel.style.display === 'block') positionHover(e);
        });
        c.addEventListener('mouseout', e => {
            if (e.target.closest('.item-opt, .spell-opt')) hoverPanel.style.display = 'none';
        });
    });

    // init
    buildGrid();
    renderSpells();
    renderGrid();
    renderBuild();
})();
