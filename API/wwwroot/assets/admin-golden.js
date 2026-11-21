/**
 * ════════════════════════════════════════════════════════════════════════════
 * DGustAR Admin Portal — Golden UX & High-Productivity Suite (Φ Ergonomics)
 * Atajos de teclado, saneamiento de emojis, y campanita indicadora visual KDS
 * Diseñado para alerta visual garantizada en cocina incluso con volumen apagado o música alta
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
  // 3. INDICADOR VISUAL DE CAMPANITA KDS (Comandas & Alertas en Cocina)
  // ─────────────────────────────────────────────────────────────────────────────
  var unreadCount = 0;
  var audioContext = null;
  var titleBlinkTimer = null;
  var originalDocTitle = document.title || 'Portal Administrativo DGustAR';
  var seenOrderIds = {};
  var prevNewOrdersCount = null;

  // Alerta en la pestaña del navegador (cuando el personal está en otra ventana)
  function startTitleFlash(count) {
    stopTitleFlash();
    originalDocTitle = document.title.replace(/^🛎️\s*\(\d+\)\s*[^•\-|]+\s*[•\-|]\s*/i, '');
    var isAlert = false;
    titleBlinkTimer = setInterval(function () {
      isAlert = !isAlert;
      document.title = isAlert
        ? '🛎️ (' + (count || unreadCount || 1) + ') ¡NUEVA COMANDA!'
        : originalDocTitle;
    }, 1200);

    window.addEventListener('focus', stopTitleFlash, { once: true });
  }

  function stopTitleFlash() {
    if (titleBlinkTimer) {
      clearInterval(titleBlinkTimer);
      titleBlinkTimer = null;
      document.title = originalDocTitle;
    }
  }

  // Chime sonoro de cortesía (sintetizado vía Web Audio API sin requerir mp3 externos)
  function playNotificationChime() {
    try {
      if (!audioContext) {
        audioContext = new (window.AudioContext || window.webkitAudioContext)();
      }
      if (audioContext.state === 'suspended') {
        audioContext.resume();
      }

      var now = audioContext.currentTime;

      // Tono 1 (Armónico brillante en Mi 659 Hz)
      var osc1 = audioContext.createOscillator();
      var gain1 = audioContext.createGain();
      osc1.type = 'sine';
      osc1.frequency.setValueAtTime(659.25, now);
      osc1.frequency.exponentialRampToValueAtTime(880, now + 0.15);
      gain1.gain.setValueAtTime(0.28, now);
      gain1.gain.exponentialRampToValueAtTime(0.001, now + 0.45);
      osc1.connect(gain1);
      gain1.connect(audioContext.destination);
      osc1.start(now);
      osc1.stop(now + 0.46);

      // Tono 2 (Campana mayor en La 880 Hz)
      var osc2 = audioContext.createOscillator();
      var gain2 = audioContext.createGain();
      osc2.type = 'sine';
      osc2.frequency.setValueAtTime(880, now + 0.18);
      osc2.frequency.exponentialRampToValueAtTime(1318.5, now + 0.35);
      gain2.gain.setValueAtTime(0.32, now + 0.18);
      gain2.gain.exponentialRampToValueAtTime(0.001, now + 0.75);
      osc2.connect(gain2);
      gain2.connect(audioContext.destination);
      osc2.start(now + 0.18);
      osc2.stop(now + 0.76);
    } catch (e) {
      // Audio bloqueado o silenciado por el navegador
    }
  }

  // Destello visual en la barra superior (captable con el rabillo del ojo)
  function flashHeader() {
    var header = document.querySelector('header[data-v-48ad60c4]');
    if (!header) return;
    header.classList.remove('header-alert-flash');
    void header.offsetWidth; // Forzar reflow para reiniciar la animación CSS
    header.classList.add('header-alert-flash');
    setTimeout(function () {
      if (header) header.classList.remove('header-alert-flash');
    }, 2800);
  }

  // Toast flotante gigante en la parte superior (Alerta visual para música alta)
  function showVisualOrderToast(notif) {
    var existingToast = document.getElementById('dgustar-order-toast');
    if (existingToast) {
      existingToast.remove();
    }

    var toast = document.createElement('div');
    toast.id = 'dgustar-order-toast';
    toast.innerHTML = [
      '<div class="toast-bell-badge">',
      '  <svg style="width:22px;height:22px;stroke:#ffffff;fill:none;stroke-width:2.2;stroke-linecap:round;stroke-linejoin:round" viewBox="0 0 24 24">',
      '    <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"></path>',
      '    <path d="M13.73 21a2 2 0 0 1-3.46 0"></path>',
      '  </svg>',
      '</div>',
      '<div class="toast-content">',
      '  <span class="toast-title">' + (notif.titulo || '¡NUEVA COMANDA EN COCINA!') + '</span>',
      '  <span class="toast-desc">' + (notif.desc || 'Comanda recibida y lista para preparar') + ' • ' + (notif.hora || 'Ahora') + '</span>',
      '</div>',
      '<button type="button" class="toast-close-btn" aria-label="Cerrar alerta">',
      '  <svg style="width:16px;height:16px;stroke:currentColor;fill:none;stroke-width:2.2" viewBox="0 0 24 24"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>',
      '</button>'
    ].join('');

    function dismissToast() {
      toast.classList.add('toast-exit');
      setTimeout(function () {
        if (toast.parentNode) toast.remove();
      }, 290);
    }

    var closeBtn = toast.querySelector('.toast-close-btn');
    if (closeBtn) closeBtn.addEventListener('click', dismissToast);

    // Si hacen clic en el banner, llevar a la pestaña de comandas
    toast.addEventListener('click', function (e) {
      if (e.target.closest('.toast-close-btn')) return;
      switchToKdsTab();
      dismissToast();
    });

    document.body.appendChild(toast);

    // Auto-ocultar después de 7 segundos
    setTimeout(function () {
      if (document.body.contains(toast)) {
        dismissToast();
      }
    }, 7000);
  }

  function switchToKdsTab() {
    var tabs = document.querySelectorAll('div[data-v-48ad60c4] .flex.flex-wrap.items-center.gap-2.p-1\\.5 button');
    if (tabs && tabs[0]) {
      tabs[0].click();
      window.scrollTo({ top: 120, behavior: 'smooth' });
    }
  }

  function updateBellUi(count) {
    if (typeof count === 'number') {
      unreadCount = count;
    }

    var indicator = document.getElementById('dgustar-kds-bell-indicator');
    var badge = document.getElementById('dgustar-kds-bell-badge');

    if (!indicator || !badge) return;

    if (unreadCount > 0) {
      badge.textContent = unreadCount > 99 ? '99+' : unreadCount;
      badge.classList.remove('hidden-badge');
      indicator.classList.add('has-alerts');
      indicator.setAttribute('title', unreadCount + ' comanda(s) pendiente(s) en cocina');
    } else {
      badge.textContent = '0';
      badge.classList.add('hidden-badge');
      indicator.classList.remove('has-alerts');
      indicator.setAttribute('title', 'Sin comandas pendientes en cocina');
    }
  }

  function triggerBellAlert(notif) {
    // 1. Sonido suave de timbre
    playNotificationChime();

    // 2. Banner toast visual en pantalla (100% visible sin audio)
    showVisualOrderToast(notif);

    // 3. Destello visual en la barra superior
    flashHeader();

    // 4. Parpadeo en el título de la pestaña del navegador
    startTitleFlash(unreadCount + 1);

    // 5. Vibración táctil si el dispositivo lo soporta
    if (navigator.vibrate) {
      navigator.vibrate([250, 120, 250, 120, 400]);
    }

    // 6. Actualizar contador visual
    unreadCount++;
    updateBellUi(unreadCount);
  }

  function ensureNotificationBell() {
    if (!isAdminRoute()) return;

    var header = document.querySelector('header[data-v-48ad60c4]');
    if (!header) return;

    var leftGroup = header.querySelector('.flex.items-center.gap-3');
    if (!leftGroup) return;

    var existing = document.getElementById('dgustar-kds-bell-indicator');
    if (existing) return;

    var indicator = document.createElement('div');
    indicator.id = 'dgustar-kds-bell-indicator';
    indicator.className = 'kds-bell-indicator';
    indicator.setAttribute('title', 'Indicador visual de comandas de cocina');
    indicator.innerHTML = [
      '<div class="kds-bell-icon-wrap">',
      '  <svg class="kds-bell-svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">',
      '    <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9"></path>',
      '    <path d="M13.73 21a2 2 0 0 1-3.46 0"></path>',
      '  </svg>',
      '  <span id="dgustar-kds-bell-badge" class="kds-bell-badge hidden-badge">0</span>',
      '</div>'
    ].join('');

    // Si el usuario hace clic en el indicador, llevar suavemente a la pestaña KDS
    indicator.addEventListener('click', function () {
      switchToKdsTab();
    });

    leftGroup.appendChild(indicator);
    updateBellUi(unreadCount);
  }

  // Sincronización continua leyendo las columnas del tablero KDS renderizado en el DOM
  function syncFromKdsBoard() {
    if (!isAdminRoute()) return;

    var countNew = 0;

    // 1. Detectar el conteo en la columna "NUEVOS (X)"
    var allElements = document.querySelectorAll('h3, div, span, button');
    for (var i = 0; i < allElements.length; i++) {
      var txt = (allElements[i].textContent || '').trim();
      var match = txt.match(/^NUEVOS\s*\((\d+)\)/i);
      if (match) {
        countNew = parseInt(match[1], 10) || 0;
        break;
      }
    }

    // Si es la primera lectura
    if (prevNewOrdersCount === null) {
      prevNewOrdersCount = countNew;
      updateBellUi(countNew);
      return;
    }

    // Si subió el número de órdenes nuevas en la columna
    if (countNew > prevNewOrdersCount) {
      var diff = countNew - prevNewOrdersCount;
      prevNewOrdersCount = countNew;
      triggerBellAlert({
        id: 'order-' + Date.now(),
        tipo: 'ORDER',
        titulo: '¡Nueva Comanda en Cocina!',
        desc: diff === 1 ? 'Se ha recibido 1 nueva comanda' : 'Se han recibido ' + diff + ' nuevas comandas',
        hora: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
      });
    } else {
      prevNewOrdersCount = countNew;
      updateBellUi(countNew);
    }
  }

  // Redundancia: Consulta periódica silenciosa a la API para pedidos nuevos
  var lastApiOrdersCheck = 0;
  async function checkOrdersApiSilently() {
    if (!isAdminRoute()) return;
    var now = Date.now();
    if (now - lastApiOrdersCheck < 4500) return;
    lastApiOrdersCheck = now;

    try {
      var token = localStorage.getItem('bistro_admin_token') || localStorage.getItem('dgustar_admin_token') || '';
      var res = await fetch('/api/admin/orders', {
        headers: { 'x-admin-token': token }
      });
      if (!res.ok) return;

      var data = await res.json();
      var orders = data.orders || [];
      var newOrders = orders.filter(function (o) {
        return o.estado === 'NUEVO' || o.estado === 'PENDIENTE';
      });

      // Detectar órdenes no vistas
      newOrders.forEach(function (ord) {
        if (!seenOrderIds[ord.id]) {
          seenOrderIds[ord.id] = true;
          // Si ya teníamos historial cargado, alertar al usuario
          if (Object.keys(seenOrderIds).length > newOrders.length) {
            var mesaName = ord.mesa || 'Mesa';
            var itemsList = (ord.detalles || []).map(function (d) {
              return (d.cantidad || 1) + 'x ' + (d.nombre || 'Platillo');
            }).join(', ');
            var summary = itemsList.length > 50 ? itemsList.substring(0, 48) + '...' : itemsList;

            triggerBellAlert({
              id: ord.id,
              tipo: 'ORDER',
              titulo: 'Comanda en ' + mesaName,
              desc: summary + (ord.total ? ' ($' + ord.total + ')' : ''),
              hora: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
            });
          }
        }
      });

      // Sincronizar el contador visual
      if (newOrders.length > 0) {
        updateBellUi(newOrders.length);
      } else if (prevNewOrdersCount === 0) {
        updateBellUi(0);
      }
    } catch (e) {
      // Ignorar fallas silenciosas
    }
  }

  // ─────────────────────────────────────────────────────────────────────────────
  // 4. CONEXIÓN WEBSOCKET DEDICADA PARA EVENTOS EN TIEMPO REAL
  // ─────────────────────────────────────────────────────────────────────────────
  var ws = null;
  function initAdminWebSocket() {
    if (!isAdminRoute()) return;
    if (ws && (ws.readyState === WebSocket.OPEN || ws.readyState === WebSocket.CONNECTING)) return;

    try {
      var protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
      var wsUrl = protocol + '//' + window.location.host + '/ws?room=kitchen';
      ws = new WebSocket(wsUrl);

      ws.onmessage = function (event) {
        try {
          var payload = JSON.parse(event.data);
          var ev = payload.event || payload.type || '';
          var data = payload.data || payload;

          if (ev === 'NEW_ORDER') {
            var mesaName = data.mesa || 'Mesa';
            var items = (data.detalles || []).map(function (d) {
              return (d.cantidad || 1) + 'x ' + (d.nombre || 'Platillo');
            }).join(', ');
            var summary = items.length > 55 ? items.substring(0, 52) + '...' : items;

            triggerBellAlert({
              id: data.id || ('order-' + Date.now()),
              tipo: 'ORDER',
              titulo: 'Comanda en ' + mesaName,
              desc: summary + (data.total ? ' ($' + data.total + ')' : ''),
              hora: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
            });
            syncFromKdsBoard();
          } else if (ev === 'WAITER_CALLED') {
            triggerBellAlert({
              id: 'call-' + Date.now(),
              tipo: 'WAITER',
              titulo: '¡Llamado en ' + (data.mesa || 'Mesa') + '!',
              desc: (data.motivo || data.comensal || 'Asistencia solicitada por comensal'),
              hora: new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
            });
          } else if (ev === 'ORDER_STATUS_CHANGED') {
            syncFromKdsBoard();
            checkOrdersApiSilently();
          }
        } catch (err) {
          // Formato auxiliar
        }
      };

      ws.onclose = function () {
        ws = null;
        setTimeout(initAdminWebSocket, 4000);
      };

      ws.onerror = function () {
        try { ws.close(); } catch (e) {}
      };
    } catch (e) {
      // Fallback a sincronización DOM y API
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

    // Escape: cerrar modal
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

  // Feedback táctil general
  document.addEventListener('click', function (e) {
    if (!isAdminRoute()) return;
    var btn = e.target.closest('button');
    if (btn && navigator.vibrate) {
      navigator.vibrate(15);
    }
  }, { passive: true });

  console.log('[DGustAR] Indicador visual de campanita KDS en vivo inicializado con éxito.');
})();
