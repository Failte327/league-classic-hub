// Tournament team registration — dynamic add/remove player-name rows, joined into a single
// hidden newline-separated field on submit (same "hidden field + JS" convention as guide-editor.js).
(function () {
    'use strict';
    const rows = document.getElementById('player-rows');
    const addBtn = document.getElementById('add-player-row');
    const hidden = document.getElementById('player-names-csv');
    const form = document.getElementById('register-form');
    if (!rows || !form) return;

    function addRow() {
        if (rows.children.length >= 20) return;
        const row = document.createElement('div');
        row.className = 'player-row';
        const input = document.createElement('input');
        input.type = 'text';
        input.className = 'player-name-input';
        input.maxLength = 60;
        input.placeholder = 'Player name (placeholder OK)';
        const remove = document.createElement('button');
        remove.type = 'button';
        remove.className = 'mini-btn danger remove-player-row';
        remove.textContent = 'Remove';
        row.appendChild(input);
        row.appendChild(remove);
        rows.appendChild(row);
    }

    addBtn.addEventListener('click', addRow);
    rows.addEventListener('click', e => {
        const btn = e.target.closest('.remove-player-row');
        if (btn) btn.closest('.player-row').remove();
    });
    form.addEventListener('submit', () => {
        const names = [...rows.querySelectorAll('.player-name-input')].map(i => i.value.trim()).filter(v => v);
        hidden.value = names.join('\n');
    });

    // A League Classic team is 5 players.
    for (let i = 0; i < 5; i++) addRow();
})();
