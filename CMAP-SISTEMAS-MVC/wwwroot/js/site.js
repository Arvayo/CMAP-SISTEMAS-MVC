/* ============================================================================
   ARCHIVO: site.js
   PROYECTO: CMAP_SISTEMAS_MVC
   DESCRIPCIÓN:
     Scripts globales del "UI Shell" (Layout)
       - Toggle sidebar (colapsar / expandir)
       - Submenús (abrir / cerrar)
       - Dark mode (body.dark)
       - Responsive: colapsar SOLO en móvil/tablet automáticamente

   DEPENDENCIAS (SELECTORES DEL _Layout.cshtml):
     - Sidebar: .menu-lateral
     - Botón toggle sidebar: .btn-sidebar-toggle
     - Botones submenú: .js-submenu-toggle
     - Padre submenú: .has-submenu
     - Toggle dark mode: .toggle-switch

   CLASES QUE ESTE SCRIPT ALTERNA:
     - .menu-lateral.close     => colapsado (desktop) / oculto (móvil)
     - .has-submenu.open       => submenú desplegado
     - body.dark               => tema oscuro
   ============================================================================ */

(() => {

    /* === CONFIGURACIÓN RESPONSIVE =========================================
       Define el ancho máximo para considerar "tablet/móvil"
       - 768px  => típico móvil grande / tablet pequeña
       - 992px  => tablet/desktop (Bootstrap lg)
       Si quieres que en tablet también se cierre de inicio, usa 992.
    ===================================================================== */
    const COLLAPSE_AT_WIDTH = 768; // <-- cambia a 992 si quieres incluir tablet

    /* === HELPERS ========================================================= */
    const qs = (sel) => document.querySelector(sel);
    const qsa = (sel) => Array.from(document.querySelectorAll(sel));

    /* === ELEMENTOS DEL DOM =============================================== */
    const sidebar = qs('.menu-lateral');
    const sidebarToggleBtn = qs('.btn-sidebar-toggle');
    const darkModeBtn = qs('.toggle-switch');
    const overlay = qs('.sidebar-overlay');

    /* =====================================================================
       /* === 1) SIDEBAR: TOGGLE MANUAL (BOTÓN) ===
       - Al hacer click en el botón, alterna la clase .close.
       - En desktop: .close = colapsado
       - En móvil:  .close = oculto (por CSS responsive)
    ===================================================================== */
    function initSidebarToggle() {
        if (!sidebar || !sidebarToggleBtn) return;

        sidebarToggleBtn.addEventListener('click', () => {
            sidebar.classList.toggle('close');
        });
    }

    /* =====================================================================
       /* === 2) SUBMENÚS: TOGGLE ===
       - Cada botón .js-submenu-toggle abre/cierra su contenedor li.has-submenu
       - Usa la clase .open en el padre
    ===================================================================== */
    function initSubmenus() {
        const submenuButtons = qsa('.js-submenu-toggle');
        if (submenuButtons.length === 0) return;

        submenuButtons.forEach((btn) => {
            btn.addEventListener('click', (e) => {
                e.preventDefault();

                const parent = btn.closest('.has-submenu');
                if (!parent) return;

                parent.classList.toggle('open');
            });
        });
    }

    /* === DARK MODE CON ICONO DINÁMICO ==================================== */
    function initDarkMode() {

        const themeBtn = qs('.btn-theme-toggle');
        const themeIcon = qs('.theme-icon');

        if (!themeBtn || !themeIcon) return;

        function updateIcon() {
            if (document.body.classList.contains('dark')) {
                themeIcon.classList.remove('bx-moon');
                themeIcon.classList.add('bx-sun');
            } else {
                themeIcon.classList.remove('bx-sun');
                themeIcon.classList.add('bx-moon');
            }
        }

        themeBtn.addEventListener('click', () => {
            document.body.classList.toggle('dark');
            updateIcon();
        });

        /* Inicializar icono al cargar */
        updateIcon();
    }

    /* =====================================================================
       /* === 4) RESPONSIVE: AUTO-COLLAPSE SOLO EN MÓVIL/TABLET ===
       OBJETIVO:
         - En PC (mayor al breakpoint): sidebar abierto por defecto
         - En móvil/tablet (menor o igual): sidebar cerrado por defecto

       NOTA IMPORTANTE:
         - Esto NO impide que el usuario lo abra con el botón.
         - Si redimensionas la ventana, se reajusta automáticamente.
    ===================================================================== */
    function applyResponsiveSidebar() {
        if (!sidebar) return;

        const isSmall = window.innerWidth <= COLLAPSE_AT_WIDTH;

        if (isSmall) {
            /* === móvil/tablet => cerrar === */
            sidebar.classList.add('close');
        } else {
            /* === desktop => abrir === */
            sidebar.classList.remove('close');
        }
    }

    /* === 5) OVERLAY: CERRAR AL HACER CLICK FUERA ========================== */
    function initOverlayClose() {
        if (!sidebar || !overlay) return;

        overlay.addEventListener('click', () => {
            if (window.innerWidth <= COLLAPSE_AT_WIDTH) {
                sidebar.classList.add('close');
            }
        });
    }

    function initResponsiveBehavior() {
        if (!sidebar) return;

        /* === ejecutar al cargar === */
        applyResponsiveSidebar();

        /* === ejecutar al cambiar tamaño === */
        window.addEventListener('resize', applyResponsiveSidebar);
    }

    /* =====================================================================
       /* === INIT GENERAL ===
    ===================================================================== */
    function init() {
        initSidebarToggle();
        initSubmenus();
        initDarkMode();
        initResponsiveBehavior();
        initOverlayClose();
    }

    /* === Ejecutar cuando el DOM esté listo === */
    document.addEventListener('DOMContentLoaded', init);

})();
