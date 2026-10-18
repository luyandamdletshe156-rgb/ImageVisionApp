// Lightweight perspective-tilt for elements marked .tilt-3d.
// No dependencies — pairs with the perspective set on .viewport in site.css.
(function () {
    function attachTilt(el) {
        var bounds;
        var maxTilt = 8; // degrees

        function onMove(e) {
            bounds = el.getBoundingClientRect();
            var relX = (e.clientX - bounds.left) / bounds.width;  // 0..1
            var relY = (e.clientY - bounds.top) / bounds.height;  // 0..1
            var rotY = (relX - 0.5) * 2 * maxTilt;
            var rotX = (0.5 - relY) * 2 * maxTilt;
            el.style.transform = 'perspective(900px) rotateX(' + rotX + 'deg) rotateY(' + rotY + 'deg) translateZ(6px)';
        }

        function onLeave() {
            el.style.transform = 'perspective(900px) rotateX(0deg) rotateY(0deg) translateZ(0px)';
        }

        el.addEventListener('mousemove', onMove);
        el.addEventListener('mouseleave', onLeave);
    }

    document.addEventListener('DOMContentLoaded', function () {
        if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
        if (window.matchMedia('(max-width: 1000px)').matches) return; // touch devices skip tilt
        document.querySelectorAll('.tilt-3d').forEach(attachTilt);
    });
})();