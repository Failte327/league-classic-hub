// Filters an item list/palette by name as the user types. Works on both the
// read-only Resources/Items grid and the clickable guide-editor item palette —
// both share the same .item-group > .item-row > .item-opt[data-name] shape.
(function () {
    'use strict';
    const input = document.getElementById('item-search');
    const list = document.getElementById('item-list');
    if (!input || !list) return;

    const groups = [...list.querySelectorAll('.item-group')];

    input.addEventListener('input', () => {
        const q = input.value.trim().toLowerCase();
        groups.forEach(group => {
            let anyVisible = false;
            group.querySelectorAll('.item-opt').forEach(opt => {
                const show = !q || (opt.dataset.name || '').toLowerCase().includes(q);
                opt.style.display = show ? '' : 'none';
                if (show) anyVisible = true;
            });
            group.style.display = anyVisible ? '' : 'none';
        });
    });
})();
