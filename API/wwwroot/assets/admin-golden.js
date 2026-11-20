/**
 * ════════════════════════════════════════════════════════════════════════════
 * DGustAR Admin Portal — Golden UX & High-Productivity Suite (Φ Ergonomics)
 * Atajos de teclado, saneamiento de emojis, campanita de notificaciones KDS en vivo
 * ════════════════════════════════════════════════════════════════════════════
 */

(function () {
  'use strict';

  function isAdminRoute() {
    return window.location.pathname.startsWith('/admin');
  }

  // ─────────────────────────────────────────────────────────────────────────────
  // 1. SANEADOR DE CATEGORÍAS Y EMOJIS (Elimina "??" y asegura emojis nítidos)
  // ─────────────────────────────────────────────────────────────────────────────
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
        if (txt.includes('??') || /^\s*\?\s+/.test(txt)) {
          var cleanName = txt.replace(/\?+/g, '').trim();
          var emoji = getCleanEmojiForText(cleanName);
          opt.textContent = emoji + ' ' + cleanName;
        }
      });
    });
  }

  // ─────────────────────────────────────────────────────────────────────────────
  // 2. AUTO-ENFOQUE INTELIGENTE EN MODALES (Ergonomía rápida)
  // ─────────────────────────────────────────────────────────────────────────────
  function checkAndEnhanceDishModal() {
    var modal = document.querySelector('.fixed.inset-0.z-50 form');
    if (!modal) return;

    sanitizeSelectOptions();

    if (!modal.dataset.enhanced) {
      modal.dataset.enhanced = 'true';
      var firstInput = modal.querySelector('input[type="text"]');
      if (firstInput && document.activeElement !== firstInput) {
        setTimeout(function () {
          firstInput.focus();
        }, 120);
      }
    }
  }

  // ─────────────────────────────────────────────────────────────────────────────
  // 3. CAMPANITA DE NOTIFICACIONES KDS EN VIVO (Pedidos y Llamados)
  // ─────────────────────────────────────────────────────────────────────────────
  var notificationsList = [];
  var unreadCount = 0;
  var audioContext = null;

  // Sonido de alerta suave para cocina (Web Audio API nativo)
  function playNotificationChime() {
    try {
      if (!audioContext) {
        audioContext = new (window.AudioContext || window.webkitAudioContext)();
      }
      if (audioContext.state === 'suspended') {
        audioContext.resume();
      }
      var now = audioContext.currentTime;

      // Nota 1: Re5 (587.33 Hz)
      var osc1 = audioContext.createOscillator();
      var gain1 = audioContext.createGain();
      osc1.type = 'sine';
      osc1.frequency.setValueAtTime(587.33, now);
      gain1.gain.setValueAtTime(0.25, now);
      gain1.gain.exponentialRampToValueAtTime(0.001, now + 0.5);
      osc1.connect(gain1);
      gain1.connect(audioContext.destination);
      osc1.start(now);
      osc1.stop(now + 0.5);

      // Nota 2: La5 (880.00 Hz) con ligero retraso para armonía
      var osc2 = audioContext.createOscillator();
      var gain2 = audioContext.createGain();
      osc2.type = 'sine';
      osc2.frequency.setValueAtTime(880.00, now + 0.12);
      gain2.gain.setValueAtTime(0.28, now + 0.12);
      gain2.gain.exponentialRampToValueAtTime(0.001, now + 0.85);
      osc2.connect(gain2);
      gain2.connect(audioContext.destination);
      osc2.start(now + 0.12);
      osc2.stop(now + 0.85);
    } catch (e) {
      // Audio no permitido hasta que el usuario interactúe con el DOM
    }
  }

  // Inyección de la campanita en el Header del Administrador
  function ensureNotificationBell() {
    if (!isAdminRoute()) return;

    var header = document.querySelector('header[data-v-48ad60c4]');
    if (!header) return;

    var leftGroup = header.querySelector('.flex.items-center.gap-3');
    if (!leftGroup) return;

    var existingWrapper = document.getElementById('dgustar-admin-bell-wrapper');
    if (existingWrapper) return;

    var wrapper = document.createElement('div');
    wrapper.id = 'dgustar-admin-bell-wrapper';
    wrapper.innerHTML = [
      '<button id="dgustar-admin-bell-btn" type="button" aria-label="Notificaciones de Pedidos" title="Notificaciones de nuevos pedidos y llamados de mesa">',
      '  <span class="material-symbols-outlined text-[20px] bell-icon">notifications</span>',
      '  <span id="dgustar-admin-bell-badge" class="hidden-badge">0</span>',
      '</button>',
      '<div id="dgustar-admin-bell-dropdown" class="hidden">',
      '  <div class="bell-dropdown-header">',
      '    <div class="bell-dropdown-title">',
      '      <span class="material-symbols-outlined text-[18px] title-icon">notifications_active</span>',
      '      <span>Alertas en Vivo</span>',
      '    </div>',
      '    <div class="bell-dropdown-actions">',
      '      <button id="dgustar-admin-bell-clear" type="button" class="bell-clear-btn">Marcar vistas</button>',
      '    </div>',
      '  </div>',
      '  <div id="dgustar-admin-bell-list" class="bell-dropdown-list">',
      '    <div class="bell-empty-state">',
      '      <span class="material-symbols-outlined">check_circle</span>',
      '      <p>No hay alertas pendientes</p>',
      '    </div>',
      '  </div>',
      '  <div class="bell-dropdown-footer">',
      '    <button id="dgustar-admin-bell-goto-kds" type="button" class="bell-footer-btn">',
      '      <span class="material-symbols-outlined text-[16px]">soup_kitchen</span>',
      '      <span>Ver Comandas en KDS</span>',
      '    </button>',
      '  </div>',
      '</div>'
    ].join('');

    // Se coloca a la derecha del título en el contenedor izquierdo
    leftGroup.appendChild(wrapper);

    // Eventos de la campanita
    var btn = document.getElementById('dgustar-admin-bell-btn');
    var dropdown = document.getElementById('dgustar-admin-bell-dropdown');
    var clearBtn = document.getElementById('dgustar-admin-bell-clear');
    var gotoKdsBtn = document.getElementById('dgustar-admin-bell-goto-kds');

    btn.addEventListener('click', function (e) {
      e.stopPropagation();
      var isHidden = dropdown.classList.contains('hidden');
      if (isHidden) {
        dropdown.classList.remove('hidden');
        btn.classList.remove('bell-shake');
      } else {
        dropdown.classList.add('hidden');
      }
    });

    clearBtn.addEventListener('click', function (e) {
      e.stopPropagation();
      clearNotifications();
    });

    gotoKdsBtn.addEventListener('click', function (e) {
      e.stopPropagation();
      dropdown.classList.add('hidden');
      switchToKdsTab();
    });

    document.addEventListener('click', function (e) {
      if (!dropdown.contains(e.target) && e.target !== btn) {
        dropdown.classList.add('hidden');
      }
    });

    syncFromKdsBoard();
  }

  function switchToKdsTab() {
    var tabs = document.querySelectorAll('div[data-v-48ad60c4] .flex.flex-wrap.items-center.gap-2.p-1\\.5 button');
    if (tabs && tabs[0]) {
      tabs[0].click();
      window.scrollTo({ top: 120, behavior: 'smooth' });
    }
  }

  function triggerBellAlert(notif) {
    playNotificationChime();

    // Añadir a la lista
    notificationsList.unshift(notif);
    if (notificationsList.length > 25) notificationsList.pop();

    unreadCount++;
    updateBellUi();

    // Animación de timbre vibrante
    var btn = document.getElementById('dgustar-admin-bell-btn');
    if (btn) {
      btn.classList.remove('bell-shake');
      void btn.offsetWidth; // Force reflow
      btn.classList.add('bell-shake');
      setTimeout(function () {
        btn.classList.remove('bell-shake');
      }, 2500);
    }
  }

  function clearNotifications() {
    unreadCount = 0;
    notificationsList = [];
    updateBellUi();
    var dropdown = document.getElementById('dgustar-admin-bell-dropdown');
    if (dropdown) dropdown.classList.add('hidden');
  }

  function updateBellUi() {
    var btn = document.getElementById('dgustar-admin-bell-btn');
    var badge = document.getElementById('dgustar-admin-bell-badge');
    var listContainer = document.getElementById('dgustar-admin-bell-list');

    if (!btn || !badge || !listContainer) return;

    if (unreadCount > 0) {
      btn.classList.add('has-alerts');
      badge.textContent = unreadCount > 99 ? '99+' : unreadCount;
      badge.classList.remove('hidden-badge');
      badge.classList.add('badge-pulse');
    } else {
      btn.classList.remove('has-alerts');
      btn.classList.remove('bell-shake');
      badge.classList.add('hidden-badge');
      badge.classList.remove('badge-pulse');
    }

    if (notificationsList.length === 0) {
      listContainer.innerHTML = [
        '<div class="bell-empty-state">',
        '  <span class="material-symbols-outlined">check_circle</span>',
        '  <p>No hay alertas pendientes</p>',
        '</div>'
      ].join('');
    } else {
      listContainer.innerHTML = notificationsList.map(function (n) {
        var isOrder = n.tipo === 'ORDER';
        var iconClass = isOrder ? 'order' : 'waiter';
        var iconName = isOrder ? 'lunch_dining' : 'notifications_active';
        return [
          '<div class="bell-item" data-id="' + (n.id || '') + '">',
          '  <div class="bell-item-icon ' + iconClass + '">',
          '    <span class="material-symbols-outlined text-[18px]">' + iconName + '</span>',
          '  </div>',
          '  <div class="bell-item-content">',
          '    <div class="bell-item-header">',
          '      <span class="bell-item-title">' + (n.titulo || 'Nueva Alerta') + '</span>',
          '      <span class="bell-item-time">' + (n.hora || 'Ahora') + '</span>',
          '    </div>',
          '    <div class="bell-item-desc">' + (n.desc || '') + '</div>',
          '  </div>',
          '</div>'
        ].join('');
      }).join('');

      // Clic en cualquier notificación salta a KDS
      listContainer.querySelectorAll('.bell-item').forEach(function (item) {
        item.addEventListener('click', function () {
          var dropdown = document.getElementById('dgustar-admin-bell-dropdown');
          if (dropdown) dropdown.classList.add('hidden');
          switchToKdsTab();
        });
      });
    }
  }

  // Sincronización en vivo desde el Tablero KDS y llamadas activas
  var prevNewOrdersCount = null;
  function syncFromKdsBoard() {
    if (!isAdminRoute()) return;

    // 1. Detectar el número en la columna NUEVOS (X)
    var count = 0;
    var columnHeadings = document.querySelectorAll('h3, div, span, button');
    columnHeadings.forEach(function (el) {
      var txt = (el.textContent || '').trim();
      var match = txt.match(/^NUEVOS\s*\((\d+)\)/i);
      if (match) {
        count = parseInt(match[1], 10) || 0;
      }
    });

    // 2. Detectar banner azul de llamado de mesero
    var waiterCalls = document.querySelectorAll('div:has(> span:contains("LLAMADO")), div.bg-blue-900\\/70, [class*="LLAMADO DE MESERO"]');
    var hasWaiterCall = document.body.innerText.includes('¡LLAMADO DE MESERO!') || document.body.innerText.includes('Solicita la Cuenta');

    // Si es la primera carga y hay comandas nuevas
    if (prevNewOrdersCount === null) {
      prevNewOrdersCount = count;
      if (count > 0) {
        unreadCount = count;
        notificationsList = [
          {
            id: 'init-orders',
            tipo: 'ORDER',
            titulo: 'Comandas Pendientes (' + count + ')',
            desc: count === 1 ? '1 pedido nuevo en cocina por preparar' : count + ' pedidos nuevos en cocina por preparar',
            hora: 'Activas'
          }
        ];
        updateBellUi();
      }
    } else if (count > prevNewOrdersCount) {
      // Llegó un pedido nuevo
      var diff = count - prevNewOrdersCount;
      prevNewOrdersCount = count;
      triggerBellAlert({
        id: 'order-' + Date.now(),
        tipo: 'ORDER',
        titulo: '¡Nuevo Pedido Recibido!',
        desc: diff === 1 ? 'Nuevo pedido registrado en cocina' : diff + ' nuevos pedidos registrados',
        hora: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
      });
    } else {
      prevNewOrdersCount = count;
    }
  }

  // ─────────────────────────────────────────────────────────────────────────────
  // 4. CONEXIÓN WEBSOCKET DEDICADA PARA EVENTOS EN TIEMPO REAL
  // ─────────────────────────────────────────────────────────────────────────────
  var ws = null;
  function initAdminWebSocket() {
    if (!isAdminRoute() || ws) return;

    var protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
    var wsUrl = protocol + '//' + window.location.host + '/ws';

    try {
      ws = new WebSocket(wsUrl);

      ws.onopen = function () {
        // Suscribirse a la sala de cocina
        ws.send(JSON.stringify({ type: 'subscribe', room: 'kitchen' }));
      };

      ws.onmessage = function (event) {
        try {
          var payload = JSON.parse(event.data);
          var ev = payload.event || payload.type;
          var data = payload.data || {};

          if (ev === 'NEW_ORDER') {
            var mesaName = data.mesa || 'Mesa';
            var itemsCount = (data.items && data.items.length) || 1;
            var summary = data.items && data.items[0]
              ? (data.items[0].cantidad || 1) + 'x ' + (data.items[0].nombre || 'Platillo') + (itemsCount > 1 ? ' y más...' : '')
              : 'Nuevo pedido en mesa';

            triggerBellAlert({
              id: data.id || ('order-' + Date.now()),
              tipo: 'ORDER',
              titulo: 'Comanda en ' + mesaName,
              desc: summary + ' ($' + (data.total || '') + ')',
              hora: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
            });
          } else if (ev === 'WAITER_CALLED') {
            triggerBellAlert({
              id: 'call-' + Date.now(),
              tipo: 'WAITER',
              titulo: '¡Llamado en ' + (data.mesa || 'Mesa') + '!',
              desc: (data.motivo || data.comensal || 'Asistencia solicitada por comensal'),
              hora: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
            });
          }
        } catch (err) {
          // Formato no JSON o evento auxiliar
        }
      };

      ws.onclose = function () {
        ws = null;
        // Reconexión automática
        setTimeout(initAdminWebSocket, 4000);
      };

      ws.onerror = function () {
        try { ws.close(); } catch (e) {}
      };
    } catch (e) {
      // Fallback a sincronización DOM
    }
  }

  // ─────────────────────────────────────────────────────────────────────────────
  // 5. ATAJOS DE TECLADO
  // ─────────────────────────────────────────────────────────────────────────────
  window.addEventListener('keydown', function (e) {
    if (!isAdminRoute()) return;

    var activeEl = document.activeElement;
    var isInInput = activeEl && (activeEl.tagName === 'INPUT' || activeEl.tagName === 'TEXTAREA' || activeEl.tagName === 'SELECT');
    var openModal = document.querySelector('.fixed.inset-0.z-50');

    // Ctrl+Enter o Cmd+Enter: Guardar platillo inmediatamente
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

    // Escape: cerrar modal o dropdown de notificaciones
    if (e.key === 'Escape') {
      var bellDropdown = document.getElementById('dgustar-admin-bell-dropdown');
      if (bellDropdown && !bellDropdown.classList.contains('hidden')) {
        bellDropdown.classList.add('hidden');
        return;
      }
      if (openModal) {
        e.preventDefault();
        var closeBtn = openModal.querySelector('button[title="Cerrar modal"], button.rounded-full');
        if (closeBtn) closeBtn.click();
        return;
      }
    }

    if (isInInput) return;

    // Teclas 1 al 6: pestañas del panel de administración
    var tabIndex = parseInt(e.key, 10);
    if (tabIndex >= 1 && tabIndex <= 6) {
      var tabButtons = document.querySelectorAll('div[data-v-48ad60c4] .flex.flex-wrap.items-center.gap-2.p-1\\.5 button');
      if (tabButtons && tabButtons[tabIndex - 1]) {
        tabButtons[tabIndex - 1].click();
        if (navigator.vibrate) navigator.vibrate(25);
      }
    }
  });

  // ─────────────────────────────────────────────────────────────────────────────
  // 6. OBSERVADOR DEL DOM Y ARRANQUE
  // ─────────────────────────────────────────────────────────────────────────────
  var observer = new MutationObserver(function () {
    if (isAdminRoute()) {
      sanitizeSelectOptions();
      checkAndEnhanceDishModal();
      ensureNotificationBell();
      syncFromKdsBoard();
    }
  });

  document.addEventListener('DOMContentLoaded', function () {
    observer.observe(document.body, { childList: true, subtree: true });
    sanitizeSelectOptions();
    ensureNotificationBell();
    initAdminWebSocket();
  });

  // Re-chequeo periódico para sincronía garantizada
  setInterval(function () {
    if (isAdminRoute()) {
      ensureNotificationBell();
      syncFromKdsBoard();
      if (!ws || ws.readyState !== WebSocket.OPEN) {
        initAdminWebSocket();
      }
    }
  }, 2500);

  // Desbloqueo de AudioContext con primer clic del usuario
  document.addEventListener('click', function () {
    if (!audioContext) {
      try {
        audioContext = new (window.AudioContext || window.webkitAudioContext)();
      } catch (e) {}
    } else if (audioContext.state === 'suspended') {
      audioContext.resume();
    }
  }, { once: true, passive: true });

  console.log('[DGustAR] High-Productivity Admin Suite Φ con Campanita KDS en vivo inicializada.');
})();
