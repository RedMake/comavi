/**
 * security-fixes.js
 * Script para corregir vulnerabilidades de sanitización en bibliotecas de terceros
 */
(function() {
  // Verificar que DOMPurify está disponible
  if (typeof DOMPurify === 'undefined') {
    console.error('DOMPurify no está cargado. La sanitización de seguridad no funcionará correctamente.');
    return;
  }

  // Configuración de DOMPurify
  const purifyConfig = {
  // Lista de etiquetas HTML permitidas ()
  ALLOWED_TAGS: [
    // Estructura básica
    'div', 'span', 'p', 'br', 'hr',
    
    // Encabezados
    'h1', 'h2', 'h3', 'h4', 'h5', 'h6',
    
    // Formato de texto
    'b', 'strong', 'i', 'em', 'mark', 'small', 'del', 'ins', 'sub', 'sup',
    
    // Listas
    'ul', 'ol', 'li', 'dl', 'dt', 'dd',
    
    // Enlaces y botones
    'a', 'button',
    
    // Tablas básicas
    'table', 'thead', 'tbody', 'tr', 'th', 'td',
    
    // Formularios (elementos seguros)
    'form', 'input', 'label', 'select', 'option', 'textarea',
    
    // Otros elementos útiles
    'code', 'pre', 'blockquote', 'cite', 'caption',
    
    // Elementos semánticos
    'article', 'section', 'nav', 'header', 'footer', 'aside', 'main',
    
    // Iconos (Font Awesome)
    'i', 'fa', 'fas', 'far', 'fab', 'fal', 'fad'
  ],
  
  // Atributos HTML permitidos
  ALLOWED_ATTR: [
    // Atributos básicos
    'id', 'class', 'style', 'title',
    
    // Enlaces
    'href', 'target', 'rel',
    
    // Imágenes y medios
    'src', 'alt', 'width', 'height',
    
    // Formularios
    'type', 'name', 'value', 'placeholder', 'checked', 'selected', 'disabled', 'readonly',
    'maxlength', 'min', 'max', 'pattern', 'required', 'autocomplete',
    
    // Datos personalizados (para JavaScript)
    'data-toggle', 'data-target', 'data-dismiss', 'data-id', 'data-whatever',
    
    // Permitir todos los atributos data-*
    'data-*',
    
    // Atributos ARIA para accesibilidad
    'role', 'aria-label', 'aria-hidden', 'aria-expanded', 'aria-controls',
    'aria-describedby', 'aria-labelledby', 'aria-current',
    
    // Permitir todos los atributos aria-*
    'aria-*',
    
    // Eventos (con cuidado, solo si realmente los necesitas)
    // 'onclick', 'onchange', 'onsubmit' // Comenta esta línea si no necesitas eventos inline
  ],
  
  // Opciones adicionales de seguridad
  ADD_ATTR: ['target'], // Permitir target="_blank" en enlaces
  ADD_URI_SAFE_ATTR: ['data-src'], // Atributos adicionales seguros para URLs
  FORBID_TAGS: ['script', 'style', 'iframe', 'object', 'embed', 'base'], // Prohibir explícitamente estas etiquetas
  FORBID_ATTR: ['onerror', 'onload', 'unload', 'onbeforeunload', 'onpagehide', 'onclick', 'ondblclick', 'onmousedown', 'onmouseup', 'onmouseover',
                'onmouseout', 'onmousemove', 'onkeydown', 'onkeyup', 'onkeypress'], // Prohibir eventos inline
  ALLOW_DATA_ATTR: true, // Permitir atributos data-*
  USE_PROFILES: { html: true }, // Usar el perfil HTML (más restrictivo que mathMl o svg)
  SANITIZE_DOM: true // Sanitizar DOM
};

  // Función de sanitización segura para HTML
  function safeHtmlSanitize(html) {
    if (html === null || html === undefined) return html;
    return DOMPurify.sanitize(String(html), purifyConfig);
  }

  // Función de escape seguro para HTML
  function safeHtmlEscape(text) {
    if (text === null || text === undefined) return text;
    return String(text)
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  // 1. Fix para DataTables
  if ($.fn.DataTable && $.fn.DataTable.ext && $.fn.DataTable.ext.internal) {
    // Corregir _stripHtml en línea 1520
    if (typeof $.fn.DataTable.ext.internal._stripHtml === 'function') {
      const originalStripHtml = $.fn.DataTable.ext.internal._stripHtml;
      $.fn.DataTable.ext.internal._stripHtml = function(d) {
        if (!d) return d;
        return safeHtmlSanitize(d);
      };
      console.log('DataTables _stripHtml: Protección aplicada');
    }

    // Corregir escape de cadenas en línea 4506
    if (typeof $.fn.DataTable.util && typeof $.fn.DataTable.util.escapeRegex === 'function') {
      const originalEscapeRegex = $.fn.DataTable.util.escapeRegex;
      $.fn.DataTable.util.escapeRegex = function(s) {
        if (!s) return s;
        // Aplicar escape de manera segura para expresiones regulares
        return s.replace(/[\-\[\]\/\{\}\(\)\*\+\?\.\\\^\$\|]/g, '\\$&');
      };
      console.log('DataTables escapeRegex: Protección aplicada');
    }

    // Otras funciones problemáticas en DataTables (líneas 14882, 14787, 6104)
    // Estas suelen ser funciones internas de procesamiento HTML
    const internalFunctions = ['_fnHtmlDecode', '_fnEscapeHtml', '_fnFilterData'];
    internalFunctions.forEach(funcName => {
      if (typeof $.fn.DataTable.ext.internal[funcName] === 'function') {
        const originalFunc = $.fn.DataTable.ext.internal[funcName];
        $.fn.DataTable.ext.internal[funcName] = function(d) {
          if (!d) return d;
          
          // Si la función espera procesar HTML, usamos DOMPurify
          // Si es una función de escape, aseguramos que se aplique correctamente
          if (funcName === '_fnFilterData') {
            // Función de filtrado - conservar texto pero eliminar HTML
            return safeHtmlSanitize(d);
          } else if (funcName === '_fnEscapeHtml') {
            // Función de escape - garantizar escape completo
            return safeHtmlEscape(d);
          } else {
            // Otras funciones - aplicar el original pero garantizar que el resultado esté sanitizado
            let result = originalFunc(d);
            return safeHtmlSanitize(result);
          }
        };
        console.log(`DataTables ${funcName}: Protección aplicada`);
      }
    });
  }

  // 2. Fix para Chart.js
  if (typeof Chart !== 'undefined') {
    // Verificar la existencia de la función problemática en Chart.js (línea 15386)
    if (Chart.helpers && Chart.helpers.escape) {
      const originalEscape = Chart.helpers.escape;
      Chart.helpers.escape = function(s) {
        if (!s) return s;
        return safeHtmlEscape(s);
      };
      console.log('Chart.js escape: Protección aplicada');
    }

    // Protección adicional para generación de tooltips en Chart.js
    if (Chart.Tooltip && Chart.Tooltip.prototype && Chart.Tooltip.prototype.generateLabels) {
      const originalTooltipGenerate = Chart.Tooltip.prototype.generateLabels;
      Chart.Tooltip.prototype.generateLabels = function(chart) {
        let labels = originalTooltipGenerate.call(this, chart);
        
        // Sanitizar cada etiqueta
        if (labels && labels.length) {
          labels.forEach(label => {
            if (label.text) {
              label.text = safeHtmlSanitize(label.text);
            }
          });
        }
        
        return labels;
      };
      console.log('Chart.js tooltips: Protección aplicada');
    }
  }

  // 3. Fix para Bootstrap (líneas 1116-1117 en bootstrap.js y bootstrap.bundle.js)
  // Bootstrap a menudo usa innerHTML o métodos que interpretan texto como HTML
  if (typeof bootstrap !== 'undefined') {
    // Proteger tooltips de Bootstrap
    if (bootstrap.Tooltip && bootstrap.Tooltip.prototype) {
      const setElementContentOriginal = bootstrap.Tooltip.prototype.setElementContent;
      if (setElementContentOriginal) {
        bootstrap.Tooltip.prototype.setElementContent = function(element, content) {
          // Si el contenido es string, sanitizar
          if (typeof content === 'string') {
            content = safeHtmlSanitize(content);
          } else if (content && content.nodeType && content.nodeType === 1) {
            // Si es un elemento DOM, sanitizar su innerHTML
            const tempDiv = document.createElement('div');
            tempDiv.appendChild(content.cloneNode(true));
            content = document.createRange().createContextualFragment(
              safeHtmlSanitize(tempDiv.innerHTML)
            );
          }
          
          return setElementContentOriginal.call(this, element, content);
        };
        console.log('Bootstrap Tooltip: Protección aplicada');
      }
    }
    
    // Proteger popover de Bootstrap
    if (bootstrap.Popover && bootstrap.Popover.prototype) {
      const setElementContentOriginal = bootstrap.Popover.prototype.setElementContent;
      if (setElementContentOriginal) {
        bootstrap.Popover.prototype.setElementContent = function(element, content) {
          // Si el contenido es string, sanitizar
          if (typeof content === 'string') {
            content = safeHtmlSanitize(content);
          } else if (content && content.nodeType && content.nodeType === 1) {
            // Si es un elemento DOM, sanitizar su innerHTML
            const tempDiv = document.createElement('div');
            tempDiv.appendChild(content.cloneNode(true));
            content = document.createRange().createContextualFragment(
              safeHtmlSanitize(tempDiv.innerHTML)
            );
          }
          
          return setElementContentOriginal.call(this, element, content);
        };
        console.log('Bootstrap Popover: Protección aplicada');
      }
    }
  }

  // 4. Protección global para métodos DOM susceptibles a XSS
  // Esta parte es útil para proteger código propio y bibliotecas no identificadas específicamente
  (function() {
    // Proteger Element.prototype.innerHTML
    const originalInnerHTMLDescriptor = Object.getOwnPropertyDescriptor(Element.prototype, 'innerHTML');
    if (originalInnerHTMLDescriptor && originalInnerHTMLDescriptor.set) {
      Object.defineProperty(Element.prototype, 'innerHTML', {
        set: function(html) {
          const sanitizedHtml = safeHtmlSanitize(html);
          originalInnerHTMLDescriptor.set.call(this, sanitizedHtml);
        },
        get: originalInnerHTMLDescriptor.get,
        configurable: true
      });
      console.log('Global DOM innerHTML: Protección aplicada');
    }

    // Proteger insertAdjacentHTML
    if (Element.prototype.insertAdjacentHTML) {
      const originalInsertAdjacentHTML = Element.prototype.insertAdjacentHTML;
      Element.prototype.insertAdjacentHTML = function(position, html) {
        const sanitizedHtml = safeHtmlSanitize(html);
        return originalInsertAdjacentHTML.call(this, position, sanitizedHtml);
      };
      console.log('Global DOM insertAdjacentHTML: Protección aplicada');
    }
    
    // No sobrescribimos document.write y document.writeln porque podría romper scripts críticos
    // En su lugar, monitorizamos su uso
    if (document.write) {
      const originalWrite = document.write;
      document.write = function() {
        console.warn('⚠️ Uso de document.write detectado. Considere usar alternativas más seguras.');
        return originalWrite.apply(this, arguments);
      };
    }
  })();

  // 5. Monitorización de posibles problemas de XSS
  window.addEventListener('error', function(e) {
    // Detectar posibles intentos de XSS que involucren script o eval
    const errorMessage = e.message || '';
    const errorStack = e.error && e.error.stack ? e.error.stack : '';
    
    if ((errorMessage + errorStack).match(/script|eval|XSS|injection/i)) {
      console.warn('⚠️ Posible intento de XSS detectado:', errorMessage);
      // Aquí podrías añadir telemetría o reportes a tu backend
    }
  });

  console.log('✅ Protección de seguridad global aplicada para bibliotecas de terceros');
})();