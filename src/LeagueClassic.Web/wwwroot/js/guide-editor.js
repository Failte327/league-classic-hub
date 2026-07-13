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
            label.textContent = ab;
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

    // init
    buildGrid();
    renderSpells();
    renderGrid();
    renderBuild();
})();
