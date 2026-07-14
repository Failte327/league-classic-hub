// Reusable hover description panel: any element with a data-desc attribute
// (and data-name) shows a floating panel on hover. Used on the guide view.
(function () {
    'use strict';
    const panel = document.createElement('div');
    panel.className = 'hover-desc';
    panel.style.display = 'none';
    document.body.appendChild(panel);

    function esc(s) { const d = document.createElement('div'); d.textContent = s || ''; return d.innerHTML; }

    // CSS zoom on <html> makes the browser read clientX/Y, offsetWidth, etc. in
    // already-zoomed ("visual") pixels, but interpret any px value WE write back
    // (style.left = ...) as pre-zoom ("logical") pixels that then get zoomed a
    // second time on render. Dividing by the zoom factor before writing cancels
    // that out. Harmless no-op when zoom is unsupported/1.
    function zoomFactor() {
        return parseFloat(getComputedStyle(document.documentElement).zoom) || 1;
    }

    function position(e) {
        const pad = 14, w = panel.offsetWidth, h = panel.offsetHeight;
        let x = e.clientX + pad, y = e.clientY + pad;
        if (x + w > window.innerWidth) x = e.clientX - w - pad;
        if (y + h > window.innerHeight) y = Math.max(4, window.innerHeight - h - pad);
        // panel is position:absolute (see classic.css), so it's relative to the
        // document, not the viewport — add scroll offsets to the viewport-relative x/y.
        const z = zoomFactor();
        panel.style.left = ((x + window.scrollX) / z) + 'px';
        panel.style.top = ((y + window.scrollY) / z) + 'px';
    }
    function show(el, e) {
        const name = el.dataset.name || el.getAttribute('title');
        const desc = el.dataset.desc;
        if (!name) return;
        panel.innerHTML = '<div class="hd-name">' + esc(name) + '</div>' +
            (desc && desc.trim()
                ? '<div class="hd-desc">' + esc(desc).replace(/\n/g, '<br>') + '</div>'
                : '<div class="hd-desc hd-none">No description available.</div>');
        panel.style.display = 'block';
        position(e);
    }

    document.addEventListener('mouseover', e => {
        const el = e.target.closest('[data-desc]');
        if (el) show(el, e);
    });
    document.addEventListener('mousemove', e => {
        if (panel.style.display !== 'block') return;
        if (e.target.closest('[data-desc]')) position(e);
        else panel.style.display = 'none';
    });
    document.addEventListener('mouseout', e => {
        if (e.target.closest('[data-desc]')) panel.style.display = 'none';
    });
})();
