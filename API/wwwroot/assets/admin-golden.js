/**
 * ════════════════════════════════════════════════════════════════════════════
 * DGustAR Admin Portal — Golden UX & High-Productivity Suite (Φ Ergonomics)
 * Atajos de teclado, saneamiento de emojis, ergonomía anti-fatiga y fluidez táctil
 * Diseñado para permitir registrar 20 platillos consecutivamente sin esfuerzo ni fatiga
 * ════════════════════════════════════════════════════════════════════════════
 */

(function () {
  'use strict';

  function isAdminRoute() {
    return window.location.pathname.startsWith('/admin');
  }

  // 1. Saneador de Categorías y Emojis (Elimina los "??" y asegura emojis nítidos)
  var EMOJI_MAP = [
    { match: /burger|hamburguesa/i, emoji: '🍔' },
    { match: /pizza/i, emoji: '🍕' },
    { match: /snack|papa/i, emoji: '🍟' },
    { match: /postre|cake|pastel|dulce/i, emoji: '🍰' },
    { match: /bebida|mocktail|drink|coctel|refresco/i, emoji: '🥤' },
    { match: /caf[eé]|coffee/i, emoji: '☕' },
    { match: /especial/i, emoji: '🍽️' }
  ];

  function getCleanEmojiForText(text) {
    for (var i = 0; i < EMOJI_MAP.length; i++) {
      if (EMOJI_MAP[i].match.test(text)) {
        return EMOJI_MAP[i].emoji;
      }
    }
    return '🍽️';
  }

  function sanitizeSelectOptions() {
    var selects = document.querySelectorAll('select');
    selects.forEach(function (sel) {
      Array.from(sel.options).forEach(function (opt) {
        var txt = opt.textContent || '';
        // Si contiene signos de interrogación corrompidos de codificación
        if (txt.includes('??') || /^\s*\?\s+/.test(txt)) {
          var cleanName = txt.replace(/\?+/g, '').trim();
          var emoji = getCleanEmojiForText(cleanName);
          opt.textContent = emoji + ' ' + cleanName;
        }
      });
    });
  }

  // 2. Auto-enfoque inteligente para registrar platillos en serie sin tocar el mouse
  function checkAndEnhanceDishModal() {
    var modal = document.querySelector('.fixed.inset-0.z-50 form');
    if (!modal) return;

    sanitizeSelectOptions();

    // Si el modal acaba de abrirse y no tiene el foco colocado aún
    if (!modal.dataset.enhanced) {
      modal.dataset.enhanced = 'true';

      // Auto-enfocar el primer campo (Nombre del Platillo)
      var firstInput = modal.querySelector('input[type="text"]');
      if (firstInput && document.activeElement !== firstInput) {
        setTimeout(function () {
          firstInput.focus();
        }, 120);
      }
    }
  }

  // 3. Atajos de teclado para ultra-velocidad y cero fatiga
  window.addEventListener('keydown', function (e) {
    if (!isAdminRoute()) return;

    var activeEl = document.activeElement;
    var isInInput = activeEl && (activeEl.tagName === 'INPUT' || activeEl.tagName === 'TEXTAREA' || activeEl.tagName === 'SELECT');
    var openModal = document.querySelector('.fixed.inset-0.z-50');

    // Ctrl+Enter o Cmd+Enter: Guardar platillo inmediatamente desde cualquier campo
    if ((e.ctrlKey || e.metaKey) && e.key === 'Enter') {
      if (openModal) {
        var submitBtn = openModal.querySelector('button[type="submit"]');
        if (submitBtn && !submitBtn.disabled) {
          e.preventDefault();
          submitBtn.click();
          if (navigator.vibrate) navigator.vibrate([20, 50, 20]);
          return;
        }
      }
    }

    // Tecla Escape: cerrar cualquier modal abierto
    if (e.key === 'Escape' && openModal) {
      e.preventDefault();
      var closeBtn = openModal.querySelector('button[title="Cerrar modal"], button.rounded-full');
      if (closeBtn) closeBtn.click();
      return;
    }

    if (isInInput) return;

    // Teclas 1 al 6: navegación rápida entre pestañas del panel de administración
    var tabIndex = parseInt(e.key, 10);
    if (tabIndex >= 1 && tabIndex <= 6) {
      var tabButtons = document.querySelectorAll('div[data-v-48ad60c4] .flex.flex-wrap.items-center.gap-2.p-1\\.5 button');
      if (tabButtons && tabButtons[tabIndex - 1]) {
        tabButtons[tabIndex - 1].click();
        if (navigator.vibrate) navigator.vibrate(25);
      }
    }
  });

  // 4. Observador continuo para sanear opciones en tiempo real cuando se renderiza el modal
  var observer = new MutationObserver(function () {
    if (isAdminRoute()) {
      sanitizeSelectOptions();
      checkAndEnhanceDishModal();
    }
  });

  document.addEventListener('DOMContentLoaded', function () {
    observer.observe(document.body, { childList: true, subtree: true });
    sanitizeSelectOptions();
  });

  // También escuchar cuando se hace click o foco en cualquier select
  document.addEventListener('focusin', function (e) {
    if (e.target && e.target.tagName === 'SELECT') {
      sanitizeSelectOptions();
    }
  });

  document.addEventListener('mousedown', function (e) {
    if (e.target && (e.target.tagName === 'SELECT' || e.target.closest('select'))) {
      sanitizeSelectOptions();
    }
  });

  // 5. Feedback táctil para botones en pantallas táctiles de cocina y mostrador
  document.addEventListener('click', function (e) {
    if (!isAdminRoute()) return;
    var btn = e.target.closest('button');
    if (btn && navigator.vibrate) {
      navigator.vibrate(15);
    }
  }, { passive: true });

  console.log('[DGustAR] High-Productivity Admin Suite Φ inicializada con éxito.');
})();

