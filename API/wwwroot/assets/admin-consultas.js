/*
 * DGustAR — Consultas del Gestor de Menú (consumen la API REST)
 *  - Consulta con filtro:  GET /api/admin/products?nombre=&categoria=&disponible=&precioMin=&precioMax=
 *  - Consulta por Id:      GET /api/admin/products/{id}
 * Se inserta debajo del encabezado "Gestor de Menú" del panel administrativo y
 * se actualiza sola cuando se agrega, edita o elimina un platillo.
 */
(function () {
  'use strict';

  var PANEL_ID = 'dgustar-consultas-menu';
  var ultimaConsulta = null; // { tipo: 'filtro' | 'id', valor: ... }

  function esRutaAdmin() {
    return /^\/admin/i.test(window.location.pathname);
  }

  function token() {
    try {
      return localStorage.getItem('bistro_admin_token') || 'bistro-admin-authorized';
    } catch (e) {
      return 'bistro-admin-authorized';
    }
  }

  function esc(texto) {
    var div = document.createElement('div');
    div.textContent = texto == null ? '' : String(texto);
    return div.innerHTML;
  }

  function dinero(valor) {
    var n = Number(valor);
    return isNaN(n) ? '' : '$' + n.toFixed(2);
  }

  function apiGet(url) {
    return fetch(url, { headers: { 'x-admin-token': token() }, cache: 'no-store' })
      .then(function (res) {
        return res.json().catch(function () { return null; }).then(function (data) {
          if (!res.ok) {
            var msg = (data && (data.error || data.message)) || ('Error ' + res.status);
            throw new Error(msg);
          }
          return data;
        });
      });
  }

  /* ---------- Estilos (en línea para no depender del CSS compilado) ---------- */
  var S = {
    panel: 'margin:16px 0;padding:18px;border-radius:24px;background:rgba(15,23,42,.85);border:1px solid rgba(148,163,184,.18);color:#e2e8f0;font-family:inherit;',
    titulo: 'display:flex;align-items:center;gap:8px;font-weight:800;font-size:15px;color:#fff;margin:0 0 4px;',
    sub: 'font-size:12px;color:#94a3b8;margin:0 0 14px;',
    fila: 'display:flex;flex-wrap:wrap;gap:10px;align-items:flex-end;margin-bottom:12px;',
    campo: 'display:flex;flex-direction:column;gap:4px;min-width:130px;flex:1;',
    label: 'font-size:10px;font-weight:800;letter-spacing:.06em;text-transform:uppercase;color:#94a3b8;',
    input: 'padding:9px 12px;border-radius:12px;border:1px solid rgba(148,163,184,.25);background:#0b1220;color:#e2e8f0;font-size:13px;outline:none;',
    btn: 'padding:9px 16px;border-radius:12px;border:0;font-weight:800;font-size:12px;cursor:pointer;display:inline-flex;align-items:center;gap:6px;',
    primario: 'background:#c8102e;color:#fff;',
    secundario: 'background:#1e293b;color:#e2e8f0;border:1px solid rgba(148,163,184,.25);',
    sep: 'height:1px;background:rgba(148,163,184,.15);margin:6px 0 14px;',
    estado: 'font-size:12px;color:#94a3b8;margin:4px 0 10px;min-height:16px;',
    tabla: 'width:100%;border-collapse:collapse;font-size:12.5px;',
    th: 'text-align:left;padding:8px;color:#94a3b8;font-size:10.5px;text-transform:uppercase;letter-spacing:.05em;border-bottom:1px solid rgba(148,163,184,.2);',
    td: 'padding:8px;border-bottom:1px solid rgba(148,163,184,.1);vertical-align:middle;',
    chipOk: 'padding:2px 8px;border-radius:999px;background:rgba(16,185,129,.15);color:#34d399;font-weight:700;font-size:11px;',
    chipNo: 'padding:2px 8px;border-radius:999px;background:rgba(239,68,68,.15);color:#f87171;font-weight:700;font-size:11px;',
    detalle: 'display:flex;gap:14px;flex-wrap:wrap;padding:14px;border-radius:16px;background:#0b1220;border:1px solid rgba(148,163,184,.2);'
  };

  function icono(nombre) {
    return '<span class="material-symbols-outlined" style="font-size:18px;">' + nombre + '</span>';
  }

  function construirPanel() {
    var panel = document.createElement('section');
    panel.id = PANEL_ID;
    panel.setAttribute('style', S.panel);
    panel.innerHTML = [
      '<h3 style="' + S.titulo + '">' + icono('manage_search') + 'Consultar platillos</h3>',
      '<p style="' + S.sub + '">Búsqueda con filtro y consulta por Id directamente a la API (Azure SQL).</p>',

      '<form data-rol="filtro" style="' + S.fila + '">',
      '  <label style="' + S.campo + 'flex:2;"><span style="' + S.label + '">Nombre o descripción</span>',
      '    <input name="nombre" type="text" placeholder="Ej. pizza" style="' + S.input + '"></label>',
      '  <label style="' + S.campo + '"><span style="' + S.label + '">Categoría</span>',
      '    <select name="categoria" style="' + S.input + '"><option value="">Todas</option></select></label>',
      '  <label style="' + S.campo + '"><span style="' + S.label + '">Disponibilidad</span>',
      '    <select name="disponible" style="' + S.input + '"><option value="">Todas</option><option value="true">Disponible</option><option value="false">Agotado</option></select></label>',
      '  <label style="' + S.campo + 'max-width:120px;"><span style="' + S.label + '">Precio mín.</span>',
      '    <input name="precioMin" type="number" min="0" step="0.01" style="' + S.input + '"></label>',
      '  <label style="' + S.campo + 'max-width:120px;"><span style="' + S.label + '">Precio máx.</span>',
      '    <input name="precioMax" type="number" min="0" step="0.01" style="' + S.input + '"></label>',
      '  <button type="submit" style="' + S.btn + S.primario + '">' + icono('filter_alt') + 'Filtrar</button>',
      '  <button type="button" data-rol="limpiar" style="' + S.btn + S.secundario + '">' + icono('close') + 'Limpiar</button>',
      '</form>',

      '<form data-rol="porId" style="' + S.fila + '">',
      '  <label style="' + S.campo + 'flex:2;"><span style="' + S.label + '">Consulta por Id</span>',
      '    <input name="id" type="text" placeholder="Ej. prod-pizza-pepperoni" style="' + S.input + '"></label>',
      '  <button type="submit" style="' + S.btn + S.primario + '">' + icono('search') + 'Buscar por Id</button>',
      '</form>',

      '<div style="' + S.sep + '"></div>',
      '<div data-rol="estado" style="' + S.estado + '"></div>',
      '<div data-rol="resultado"></div>'
    ].join('');

    panel.querySelector('form[data-rol="filtro"]').addEventListener('submit', function (e) {
      e.preventDefault();
      consultarConFiltro(leerFiltro(panel));
    });
    panel.querySelector('button[data-rol="limpiar"]').addEventListener('click', function () {
      panel.querySelector('form[data-rol="filtro"]').reset();
      panel.querySelector('form[data-rol="porId"]').reset();
      ultimaConsulta = null;
      panel.querySelector('[data-rol="resultado"]').innerHTML = '';
      setEstado('');
    });
    panel.querySelector('form[data-rol="porId"]').addEventListener('submit', function (e) {
      e.preventDefault();
      var id = panel.querySelector('input[name="id"]').value.trim();
      if (!id) { setEstado('Escribe el Id del platillo.', true); return; }
      consultarPorId(id);
    });
    panel.querySelector('[data-rol="resultado"]').addEventListener('click', function (e) {
      var btn = e.target.closest('button[data-id]');
      if (!btn) return;
      panel.querySelector('input[name="id"]').value = btn.getAttribute('data-id');
      consultarPorId(btn.getAttribute('data-id'));
    });

    cargarCategorias(panel);
    return panel;
  }

  function leerFiltro(panel) {
    var f = panel.querySelector('form[data-rol="filtro"]');
    return {
      nombre: f.nombre.value.trim(),
      categoria: f.categoria.value,
      disponible: f.disponible.value,
      precioMin: f.precioMin.value,
      precioMax: f.precioMax.value
    };
  }

  var categorias = {};

  function cargarCategorias(panel) {
    apiGet('/api/admin/categories').then(function (lista) {
      var select = panel.querySelector('select[name="categoria"]');
      (lista || []).forEach(function (c) {
        categorias[c.id] = (c.emoji ? c.emoji + ' ' : '') + c.nombre;
        var op = document.createElement('option');
        op.value = c.id;
        op.textContent = categorias[c.id];
        select.appendChild(op);
      });
    }).catch(function () { /* el filtro funciona aunque no carguen las categorías */ });
  }

  function setEstado(texto, error) {
    var el = document.querySelector('#' + PANEL_ID + ' [data-rol="estado"]');
    if (!el) return;
    el.textContent = texto;
    el.style.color = error ? '#f87171' : '#94a3b8';
  }

  function pintar(html) {
    var el = document.querySelector('#' + PANEL_ID + ' [data-rol="resultado"]');
    if (el) el.innerHTML = html;
  }

  function consultarConFiltro(filtro) {
    var params = new URLSearchParams();
    Object.keys(filtro).forEach(function (k) {
      if (filtro[k] !== '' && filtro[k] != null) params.append(k, filtro[k]);
    });
    var url = '/api/admin/products' + (params.toString() ? '?' + params.toString() : '');
    ultimaConsulta = { tipo: 'filtro', valor: filtro };
    setEstado('Consultando ' + url + ' …');

    return apiGet(url).then(function (lista) {
      lista = lista || [];
      setEstado('GET ' + url + '  →  ' + lista.length + ' resultado(s)');
      if (lista.length === 0) {
        pintar('<p style="font-size:13px;color:#94a3b8;">No hay platillos que coincidan con el filtro.</p>');
        return;
      }
      pintar(
        '<div style="overflow-x:auto;"><table style="' + S.tabla + '"><thead><tr>' +
        '<th style="' + S.th + '">Id</th><th style="' + S.th + '">Nombre</th><th style="' + S.th + '">Categoría</th>' +
        '<th style="' + S.th + '">Precio</th><th style="' + S.th + '">Estado</th><th style="' + S.th + '"></th>' +
        '</tr></thead><tbody>' +
        lista.map(function (p) {
          return '<tr>' +
            '<td style="' + S.td + 'font-family:monospace;color:#94a3b8;">' + esc(p.id) + '</td>' +
            '<td style="' + S.td + 'font-weight:700;color:#fff;">' + esc(p.nombre) + '</td>' +
            '<td style="' + S.td + '">' + esc(categorias[p.categoria_id] || p.categoria_id) + '</td>' +
            '<td style="' + S.td + '">' + dinero(p.precio_base) + '</td>' +
            '<td style="' + S.td + '"><span style="' + (p.disponible ? S.chipOk : S.chipNo) + '">' + (p.disponible ? 'Disponible' : 'Agotado') + '</span></td>' +
            '<td style="' + S.td + 'text-align:right;"><button type="button" data-id="' + esc(p.id) + '" style="' + S.btn + S.secundario + 'padding:6px 10px;">' + icono('visibility') + 'Ver</button></td>' +
            '</tr>';
        }).join('') +
        '</tbody></table></div>'
      );
    }).catch(function (err) {
      setEstado('No se pudo consultar: ' + err.message, true);
      pintar('');
    });
  }

  function consultarPorId(id) {
    var url = '/api/admin/products/' + encodeURIComponent(id);
    ultimaConsulta = { tipo: 'id', valor: id };
    setEstado('Consultando ' + url + ' …');

    return apiGet(url).then(function (p) {
      setEstado('GET ' + url + '  →  encontrado');
      var img = p.imagen ? '<img src="' + esc(p.imagen) + '" alt="" style="width:120px;height:120px;object-fit:cover;border-radius:14px;">' : '';
      pintar(
        '<div style="' + S.detalle + '">' + img +
        '<div style="flex:1;min-width:220px;display:grid;grid-template-columns:auto 1fr;gap:6px 14px;font-size:13px;">' +
        fila('Id', '<span style="font-family:monospace;">' + esc(p.id) + '</span>') +
        fila('Nombre', '<b style="color:#fff;">' + esc(p.nombre) + '</b>') +
        fila('Categoría', esc(categorias[p.categoria_id] || p.categoria_id)) +
        fila('Precio', dinero(p.precio_base)) +
        fila('Preparación', esc(p.tiempo_preparacion) + ' min') +
        fila('Estado', '<span style="' + (p.disponible ? S.chipOk : S.chipNo) + '">' + (p.disponible ? 'Disponible' : 'Agotado') + '</span>') +
        fila('Descripción', esc(p.descripcion || '—')) +
        '</div></div>'
      );
    }).catch(function (err) {
      setEstado('GET ' + url + '  →  ' + err.message, true);
      pintar('');
    });
  }

  function fila(etiqueta, valor) {
    return '<span style="color:#94a3b8;font-weight:700;">' + etiqueta + '</span><span>' + valor + '</span>';
  }

  /* ---------- Actualización automática tras agregar / editar / eliminar ---------- */
  var refrescoPendiente = null;
  function refrescarConsulta() {
    if (!ultimaConsulta || !document.getElementById(PANEL_ID)) return;
    clearTimeout(refrescoPendiente);
    refrescoPendiente = setTimeout(function () {
      if (!ultimaConsulta) return;
      if (ultimaConsulta.tipo === 'filtro') consultarConFiltro(ultimaConsulta.valor);
      else consultarPorId(ultimaConsulta.valor);
    }, 400);
  }

  var fetchOriginal = window.fetch;
  window.fetch = function (recurso, opciones) {
    var url = typeof recurso === 'string' ? recurso : (recurso && recurso.url) || '';
    var metodo = ((opciones && opciones.method) || (recurso && recurso.method) || 'GET').toUpperCase();
    var promesa = fetchOriginal.apply(this, arguments);
    if (metodo !== 'GET' && /\/api\/admin\/products/i.test(url)) {
      promesa.then(function (res) { if (res && res.ok) refrescarConsulta(); }).catch(function () {});
    }
    return promesa;
  };

  /* ---------- Insertar el panel en la pestaña "Gestor de Menú" ---------- */
  function encabezadoGestor() {
    var titulos = document.querySelectorAll('h2');
    for (var i = 0; i < titulos.length; i++) {
      if (/Gestor de Men/i.test(titulos[i].textContent || '')) return titulos[i];
    }
    return null;
  }

  function contenedorEncabezado(h2) {
    // Sube hasta el bloque que también contiene el botón "Nuevo Platillo"
    var el = h2;
    for (var i = 0; i < 6 && el && el.parentElement; i++) {
      el = el.parentElement;
      var botones = el.querySelectorAll('button');
      for (var j = 0; j < botones.length; j++) {
        if (/Nuevo Platillo/i.test(botones[j].textContent || '')) return el;
      }
    }
    return h2.parentElement && h2.parentElement.parentElement;
  }

  function asegurarPanel() {
    if (!esRutaAdmin()) return;
    var h2 = encabezadoGestor();
    var existente = document.getElementById(PANEL_ID);
    if (!h2) return;
    if (existente && existente.isConnected) return;
    var bloque = contenedorEncabezado(h2);
    if (!bloque || !bloque.parentNode) return;
    bloque.parentNode.insertBefore(construirPanel(), bloque.nextSibling);
    ultimaConsulta = null;
  }

  var observador = new MutationObserver(function () {
    asegurarPanel();
  });

  function iniciar() {
    asegurarPanel();
    observador.observe(document.body, { childList: true, subtree: true });
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', iniciar);
  else iniciar();
})();
