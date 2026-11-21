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
  var titleBlinkTimer = null;
  var originalDocTitle = document.title || 'Portal Administrativo DGustAR';
  var seenOrderIds = {};

  // Alerta en la pestaña del navegador (útil cuando el admin está en otra ventana)
  function startTitleFlash(alertText) {
    stopTitleFlash();
    originalDocTitle = document.title.replace(/^🛎️\s*\(\d+\)\s*[^•\-|]+\s*[•\-|]\s*/i, '');
    var isAlert = false;
    titleBlinkTimer = setInterval(function () {
      isAlert = !isAlert;
      document.title = isAlert
        ? '🛎️ (' + (unreadCount || 1) + ') ¡NUEVA COMANDA!'
        : originalDocTitle;
    }, 1200);

    window.addEventListener('focus', stopTitleFlash, { once: true });
  }

  function stopTitleFlash() {
    if (titleBlinkTimer) {
      clearInterval(titleBlinkTimer);
      titleBlinkTimer = null;
    }
    if (originalDocTitle) {
      document.title = originalDocTitle;
    }
  }

  // Destello visual en la barra superior
  function flashHeader() {
    var header = document.querySelector('header[data-v-48ad60c4]');
    if (header) {
      header.classList.remove('header-alert-flash');
      void header.offsetWidth; // Forzar reflow
      header.classList.add('header-alert-flash');
      setTimeout(function () {
        header.classList.remove('header-alert-flash');
      }, 2600);
    }
  }

  // Gran Banner Toast flotante en pantalla (Alerta 100% visual sin depender del volumen)
  function showVisualOrderToast(notif) {
    // Si ya hay un toast en pantalla, removerlo
    var existingToast = document.getElementById('dgustar-order-toast');
    if (existingToast) {
      existingToast.remove();
    }

    var isOrder = notif.tipo === 'ORDER';
    var iconName = isOrder ? 'lunch_dining' : 'notifications_active';
    var badgeTitle = isOrder ? '¡NUEVA COMANDA RECIBIDA!' : '¡LLAMADO DE MESERO!';

    var toast = document.createElement('div');
    toast.id = 'dgustar-order-toast';
    toast.innerHTML = [
      '<div class="toast-icon-wrap">',
      '  <span class="material-symbols-outlined text-[24px]">' + iconName + '</span>',
      '</div>',
      '<div class="toast-body">',
      '  <div class="toast-title">',
      '    <span class="material-symbols-outlined text-[16px]">bolt</span>',
      '    <span>' + badgeTitle + '</span>',
      '  </div>',
      '  <div class="toast-desc">' + (notif.titulo ? notif.titulo + ' • ' : '') + (notif.desc || '') + '</div>',
      '</div>',
      '<div class="toast-actions">',
      '  <button id="dgustar-toast-view-btn" type="button" class="toast-btn-action">Ver Comanda</button>',
      '  <button id="dgustar-toast-close-btn" type="button" class="toast-btn-close" aria-label="Cerrar">✕</button>',
      '</div>'
    ].join('');

    document.body.appendChild(toast);

    var viewBtn = document.getElementById('dgustar-toast-view-btn');
    var closeBtn = document.getElementById('dgustar-toast-close-btn');

    function closeToast() {
      toast.classList.add('toast-hiding');
      setTimeout(function () {
        if (toast.parentNode) toast.parentNode.removeChild(toast);
      }, 260);
    }

    if (viewBtn) {
      viewBtn.addEventListener('click', function (e) {
        e.stopPropagation();
        closeToast();
        stopTitleFlash();
        switchToKdsTab();
      });
    }

    if (closeBtn) {
      closeBtn.addEventListener('click', function (e) {
        e.stopPropagation();
        closeToast();
        stopTitleFlash();
      });
    }

    toast.addEventListener('click', function () {
      closeToast();
      stopTitleFlash();
      switchToKdsTab();
    });

    // Auto-cierre tras 12 segundos
    setTimeout(function () {
      if (document.body.contains(toast) && !toast.classList.contains('toast-hiding')) {
        closeToast();
      }
    }, 12000);
  }

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
      stopTitleFlash();
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
      stopTitleFlash();
      switchToKdsTab();
    });

    document.addEventListener('click', function (e) {
      if (!dropdown.contains(e.target) && e.target !== btn) {
        dropdown.classList.add('hidden');
      }
    });

    updateBellUi();
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
    // 1. Sonido
    playNotificationChime();

    // 2. Alerta visual en pantalla (Banner Flotante)
    showVisualOrderToast(notif);

    // 3. Destello en cabecera
    flashHeader();

    // 4. Parpadeo en la pestaña del navegador
    startTitleFlash(notif.titulo || 'Nueva Comanda');

    // 5. Vibración si el hardware lo soporta
    if (navigator.vibrate) {
      navigator.vibrate([250, 120, 250, 120, 400]);
    }

    // 6. Añadir a la lista de alertas
    notificationsList.unshift(notif);
    if (notificationsList.length > 25) notificationsList.pop();

    unreadCount++;
    updateBellUi();

    // 7. Animación de timbre vibrante
    var btn = document.getElementById('dgustar-admin-bell-btn');
    if (btn) {
      btn.classList.remove('bell-shake');
      void btn.offsetWidth; // Force reflow
      btn.classList.add('bell-shake');
      setTimeout(function () {
        btn.classList.remove('bell-shake');
      }, 3500);
    }
  }

  function clearNotifications() {
    unreadCount = 0;
    notificationsList = [];
    stopTitleFlash();
    updateBellUi();
    var dropdown = document.getElementById('dgustar-admin-bell-dropdown');
    if (dropdown) dropdown.classList.add('hidden');
  }

  function updateBellUi() {
    var btn = document.getElementById('dgustar-admin-bell-btn');
    var badge = document.getElementById('dgustar-admin-bell-badge');
    var listContainer = document.getElementById('dgustar-admin-bell-list');

    if (!btn || !badge) return;

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

    if (!listContainer) return;

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
          stopTitleFlash();
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
    var countNew = 0;
    var columnHeadings = document.querySelectorAll('h3, div, span, button');
    columnHeadings.forEach(function (el) {
      var txt = (el.textContent || '').trim();
      var match = txt.match(/^NUEVOS\s*\((\d+)\)/i);
      if (match) {
        countNew = parseInt(match[1], 10) || 0;
      }
    });

    // 2. Detectar si hay órdenes activas en el botón de la pestaña KDS
    var activeTabCount = 0;
    var kdsButton = Array.from(document.querySelectorAll('button')).find(function (b) {
      return b.textContent && b.textContent.includes('Comandas Cocina');
    });
    if (kdsButton) {
      var matchBtn = kdsButton.textContent.match(/Comandas Cocina\s*\(KDS\)\s*(\d+)/i);
      if (matchBtn) {
        activeTabCount = parseInt(matchBtn[1], 10) || 0;
      }
    }

    // Si es la primera carga y hay comandas nuevas
    if (prevNewOrdersCount === null) {
      prevNewOrdersCount = countNew;
      if (countNew > 0 && unreadCount === 0) {
        unreadCount = countNew;
        notificationsList = [
          {
            id: 'init-orders',
            tipo: 'ORDER',
            titulo: 'Comandas Pendientes (' + countNew + ')',
            desc: countNew === 1 ? '1 pedido nuevo en cocina por preparar' : countNew + ' pedidos nuevos en cocina por preparar',
            hora: 'Activas'
          }
        ];
        updateBellUi();
      }
    } else if (countNew > prevNewOrdersCount) {
      // Llegó un pedido nuevo
      var diff = countNew - prevNewOrdersCount;
      prevNewOrdersCount = countNew;
      triggerBellAlert({
        id: 'order-' + Date.now(),
        tipo: 'ORDER',
        titulo: '¡Nuevo Pedido Recibido!',
        desc: diff === 1 ? 'Nuevo pedido registrado en cocina' : diff + ' nuevos pedidos registrados',
        hora: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
      });
    } else {
      prevNewOrdersCount = countNew;
    }
  }

  // Consulta periódica directa a la API para redundancia infalible
  var lastApiOrdersCheck = 0;
  async function checkOrdersApiSilently() {
    if (!isAdminRoute()) return;
    var now = Date.now();
    if (now - lastApiOrdersCheck < 4000) return;
    lastApiOrdersCheck = now;

    try {
      var token = localStorage.getItem('bistro_admin_token') || '';
      var res = await fetch('/api/admin/orders', {
        headers: { 'x-admin-token': token }
      });
      if (!res.ok) return;

      var data = await res.json();
      var orders = data.orders || [];
      var newOrders = orders.filter(function (o) {
        return o.estado === 'PENDIENTE';
      });

      // Registrar cualquier orden que no hayamos visto antes
      newOrders.forEach(function (ord) {
        if (!seenOrderIds[ord.id]) {
          seenOrderIds[ord.id] = true;
          // Solo alertar si no es la primera carga fría
          if (Object.keys(seenOrderIds).length > newOrders.length) {
            var mesaName = ord.mesa || 'Mesa';
            var firstItem = ord.items && ord.items[0];
            var summary = firstItem
              ? (firstItem.cantidad || 1) + 'x ' + (firstItem.nombre || 'Platillo')
              : 'Nuevo pedido';

            triggerBellAlert({
              id: ord.id,
              tipo: 'ORDER',
              titulo: 'Comanda en ' + mesaName,
              desc: summary + ' ($' + (ord.total || '') + ')',
              hora: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
            });
          }
        }
      });

      // Si hay órdenes nuevas pendientes y unreadCount es 0, mostrar el número
      if (newOrders.length > 0 && unreadCount === 0) {
        unreadCount = newOrders.length;
        updateBellUi();
      }
    } catch (e) {
      // Ignorar fallas de red
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
      checkOrdersApiSilently();
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
