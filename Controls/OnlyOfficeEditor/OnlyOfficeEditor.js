/**
 * OnlyOfficeEditorModule — Módulo JavaScript para OnlyOffice Document Server.
 */
var OnlyOfficeEditorModule = (function () {
    'use strict';

    var _instances = {};

    function _busyId(containerId) {
        return containerId + '_busy';
    }

    function _setBusy(containerId, isBusy) {
        try {
            var el = document.getElementById(_busyId(containerId));
            if (el) el.style.display = isBusy ? 'flex' : 'none';
        } catch (e) { }
    }

    function init(containerId, config, options) {
        if (!config || !config.document || !config.document.url) {
            console.warn('[OnlyOfficeEditorModule] Config inválida o sin document.url');
            return null;
        }

        if (typeof DocsAPI === 'undefined') {
            console.error('[OnlyOfficeEditorModule] DocsAPI no está cargado.');
            if (options && options.onError) options.onError({ message: 'DocsAPI not loaded' });
            return null;
        }

        options = options || {};

        if (_instances[containerId]) {
            destroy(containerId);
        }

        _setBusy(containerId, true);

        config.editorConfig = config.editorConfig || {};
        config.editorConfig.customization = config.editorConfig.customization || {};
        var cust = config.editorConfig.customization;
        cust.review = cust.review || {};

        if (cust.uiTheme === undefined) cust.uiTheme = 'theme-classic-light';
        if (cust.compactToolbar === undefined) cust.compactToolbar = false;
        if (cust.toolbarNoTabs === undefined) cust.toolbarNoTabs = false;
        if (cust.hideRightMenu === undefined) cust.hideRightMenu = true;
        if (cust.hideRulers === undefined) cust.hideRulers = true;
        if (cust.review.showReviewChanges === undefined) {
            cust.review.showReviewChanges = cust.showReviewChanges === undefined
                ? false
                : !!cust.showReviewChanges;
        }
        if (cust.showReviewChanges !== undefined) delete cust.showReviewChanges;

        var _downloadResolve = null;
        var _downloadReject = null;

        config.events = config.events || {};

        config.events.onAppReady = function () {
            _setBusy(containerId, false);
            if (options.onReady) options.onReady();
        };

        config.events.onDocumentReady = function () {
            if (options.onDocumentReady) options.onDocumentReady();
        };

        config.events.onDownloadAs = function (evt) {
            _setBusy(containerId, false);
            var url = null;
            try {
                var data = evt && evt.data;
                url = (data && typeof data === 'object') ? data.url : data;
            } catch (e) { }

            if (_downloadResolve) {
                _downloadResolve(url);
                _downloadResolve = null;
                _downloadReject = null;
            }
        };

        config.events.onError = function (evt) {
            _setBusy(containerId, false);
            if (_downloadReject) {
                _downloadReject(evt);
                _downloadResolve = null;
                _downloadReject = null;
            }
            if (options.onError) options.onError(evt);
        };

        var editor = new DocsAPI.DocEditor(containerId, config);

        _instances[containerId] = {
            editor: editor,
            setDownloadResolvers: function (resolve, reject) {
                _downloadResolve = resolve;
                _downloadReject = reject;
            }
        };

        setTimeout(function () { _setBusy(containerId, false); }, 8000);

        return editor;
    }

    function getEditedDocumentUrl(containerId) {
        return new Promise(function (resolve, reject) {
            var instance = _instances[containerId];
            if (!instance || !instance.editor) {
                reject(new Error('Editor no inicializado para: ' + containerId));
                return;
            }

            instance.setDownloadResolvers(resolve, reject);
            _setBusy(containerId, true);

            try {
                instance.editor.downloadAs();
            } catch (e) {
                _setBusy(containerId, false);
                instance.setDownloadResolvers(null, null);
                reject(e);
            }

            setTimeout(function () {
                if (_instances[containerId] && _instances[containerId].setDownloadResolvers) {
                    _setBusy(containerId, false);
                }
            }, 30000);
        });
    }

    function getEditedDocumentBlob(containerId) {
        return getEditedDocumentUrl(containerId).then(function (url) {
            if (!url) throw new Error('No se recibió URL de descarga');

            var proxyBase = window.__onlyOfficeProxyUrl;
            var fetchUrl = proxyBase
                ? proxyBase + encodeURIComponent(url)
                : url;

            return fetch(fetchUrl).then(function (response) {
                if (!response.ok) throw new Error('Error al descargar: ' + response.status);
                return response.blob();
            });
        });
    }

    function getEditor(containerId) {
        var instance = _instances[containerId];
        return instance ? instance.editor : null;
    }

    function destroy(containerId) {
        var instance = _instances[containerId];
        if (instance && instance.editor) {
            try { instance.editor.destroyEditor(); } catch (e) { }
        }
        delete _instances[containerId];
    }

    function captureToHiddenField(containerId, hiddenFieldId, options) {
        options = options || {};
        _setBusy(containerId, true);

        return getEditedDocumentBlob(containerId)
            .then(function (blob) {
                return new Promise(function (resolve, reject) {
                    var reader = new FileReader();
                    reader.onload = function () {
                        var dataUrl = reader.result;
                        var base64 = dataUrl.indexOf(',') >= 0
                            ? dataUrl.substring(dataUrl.indexOf(',') + 1)
                            : dataUrl;
                        resolve(base64);
                    };
                    reader.onerror = function () { reject(reader.error); };
                    reader.readAsDataURL(blob);
                });
            })
            .then(function (base64) {
                var hf = document.getElementById(hiddenFieldId);
                if (!hf) throw new Error('HiddenField no encontrado: ' + hiddenFieldId);
                hf.value = base64;

                _setBusy(containerId, false);

                if (options.onCaptured) options.onCaptured(base64);

                if (options.autoPostBack && typeof __doPostBack === 'function') {
                    __doPostBack(options.postBackTarget || '', options.postBackArgument || '');
                }

                return base64;
            })
            .catch(function (err) {
                _setBusy(containerId, false);
                if (options.onError) options.onError(err);
                throw err;
            });
    }

    return {
        init: init,
        getEditedDocumentUrl: getEditedDocumentUrl,
        getEditedDocumentBlob: getEditedDocumentBlob,
        getEditor: getEditor,
        destroy: destroy,
        captureToHiddenField: captureToHiddenField
    };
})();
