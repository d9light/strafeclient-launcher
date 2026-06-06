document.addEventListener('DOMContentLoaded', () => {
    // ==========================================
    // ==========================================

    // ==========================================
    // TOAST NOTIFICATION SYSTEM
    // ==========================================
    const toastContainer = document.getElementById('toast-container');

    function showToast(message, type = 'info') {
        const icons = {
            success: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path><polyline points="22 4 12 14.01 9 11.01"></polyline></svg>',
            error: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="15" y1="9" x2="9" y2="15"></line><line x1="9" y1="9" x2="15" y2="15"></line></svg>',
            info: '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="12" cy="12" r="10"></circle><line x1="12" y1="16" x2="12" y2="12"></line><line x1="12" y1="8" x2="12.01" y2="8"></line></svg>'
        };

        const toast = document.createElement('div');
        toast.className = `toast toast--${type}`;
        toast.innerHTML = `
            <span class="toast-icon">${icons[type] || icons.info}</span>
            <span>${message}</span>
            <div class="toast-progress" style="animation-duration: 4s;"></div>
        `;
        toastContainer.appendChild(toast);

        setTimeout(() => {
            toast.classList.add('removing');
            setTimeout(() => toast.remove(), 300);
        }, 4000);
    }

    // ==========================================
    // EMAIL VERIFICATION BANNER
    // ==========================================
    function showEmailVerificationBanner(email) {
        var old = document.getElementById('ev-wrapper');
        if (old) old.remove();

        var masked = email.replace(/(.{2})(.*)(@.*)/, function(_, a, b, c) {
            return a + '*'.repeat(Math.min(b.length, 6)) + c;
        });

        var wrapper = document.createElement('div');
        wrapper.id = 'ev-wrapper';
        wrapper.style.cssText = 'position:fixed;inset:0;z-index:9999;display:flex;align-items:center;justify-content:center;';

        var html = '<style>';
        html += '#ev-wrapper { background: rgba(0,0,0,0.6); backdrop-filter: blur(8px); animation: fadeIn 0.3s ease; }';
        html += '@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }';
        html += '#email-verify-banner { width: 420px; max-width: 90vw; background: linear-gradient(135deg, rgba(16,16,28,0.95), rgba(28,18,46,0.95)); border: 1px solid rgba(139,92,246,0.4); border-radius: 20px; padding: 36px 32px; box-shadow: 0 0 60px rgba(139,92,246,0.2), 0 25px 50px rgba(0,0,0,0.5); text-align: center; font-family: "Poppins", sans-serif; position: relative; animation: evIn 0.4s cubic-bezier(0.34,1.56,0.64,1) both; }';
        html += '@keyframes evIn { from { transform: scale(0.9); opacity: 0; } to { transform: scale(1); opacity: 1; } }';
        html += '@keyframes evPulse { 0%,100% { transform: translateY(0) rotate(-3deg); } 50% { transform: translateY(-7px) rotate(-3deg); } }';
        html += '.ev-icon { font-size: 3.5rem; display: inline-block; animation: evPulse 2.2s ease-in-out infinite; margin-bottom: 14px; }';
        html += '.ev-title { color: #e2e8f0; font-size: 1.3rem; font-weight: 700; margin-bottom: 8px; }';
        html += '.ev-sub { color: #94a3b8; font-size: 0.9rem; line-height: 1.5; margin-bottom: 4px; }';
        html += '.ev-chip { color: #a78bfa; font-weight: 600; font-size: 0.95rem; background: rgba(139,92,246,0.15); padding: 6px 16px; border-radius: 8px; display: inline-block; margin: 12px 0 20px; border: 1px solid rgba(139,92,246,0.3); }';
        html += '.ev-btn { width: 100%; padding: 14px; border-radius: 10px; font-size: 0.9rem; font-weight: 600; cursor: pointer; transition: all 0.2s; font-family: "Poppins", sans-serif; margin-bottom: 12px; border: none; }';
        html += '.ev-primary { background: linear-gradient(135deg, #7c3aed, #5b21b6); color: white; box-shadow: 0 4px 15px rgba(124,58,237,0.3); }';
        html += '.ev-primary:hover { transform: translateY(-2px); box-shadow: 0 6px 20px rgba(124,58,237,0.4); filter: brightness(1.1); }';
        html += '.ev-secondary { background: rgba(255,255,255,0.05); color: #cbd5e1; border: 1px solid rgba(255,255,255,0.1); }';
        html += '.ev-secondary:hover { background: rgba(255,255,255,0.1); border-color: rgba(255,255,255,0.2); color: white; }';
        html += '.ev-hint { color: #64748b; font-size: 0.8rem; margin-top: 15px; }';
        html += '</style>';
        
        html += '<div id="email-verify-banner" onclick="event.stopPropagation()">';
        html += '<div class="ev-icon">&#x1F4E8;</div>';
        html += '<div class="ev-title">Verifique seu e-mail</div>';
        html += '<p class="ev-sub">Enviamos um link de ativacao para:</p>';
        html += '<div class="ev-chip">' + masked + '</div>';
        html += '<p class="ev-sub">Clique no link para ativar sua conta no <strong style="color:#a78bfa">Strafe Client</strong>.</p>';
        html += '<button class="ev-btn ev-primary" id="ev-resend-btn">&#x1F4E7; Reenviar e-mail de verificacao</button>';
        html += '<button class="ev-btn ev-secondary" id="ev-close-btn">Entendi, vou verificar minha caixa</button>';
        html += '<p class="ev-hint">Nao encontrou? Verifique a pasta de spam.</p>';
        html += '</div>';

        wrapper.innerHTML = html;
        document.body.appendChild(wrapper);

        wrapper.addEventListener('click', function() { wrapper.remove(); });
        document.getElementById('ev-close-btn').addEventListener('click', function() { wrapper.remove(); });
        document.getElementById('ev-resend-btn').addEventListener('click', async function() {
            var btn = document.getElementById('ev-resend-btn');
            btn.textContent = 'Enviando...'; btn.disabled = true;
            try {
                var res = await fetch('https://brlaucher-api.vercel.app/api/auth/reenviar-verificacao', {
                    method: 'POST', headers: {'Content-Type': 'application/json'},
                    body: JSON.stringify({ email: email })
                });
                if (res.ok) { btn.textContent = 'E-mail reenviado!'; showToast('E-mail reenviado com sucesso!', 'success'); }
                else {
                    var d = await res.json().catch(function(){ return {}; });
                    btn.textContent = d.mensagem || 'Falha ao reenviar';
                    btn.disabled = false;
                }
            } catch(e) { btn.textContent = 'Erro de conexao'; btn.disabled = false; }
        });
    }

    // ==========================================
    // WINDOW CONTROLS
    // ==========================================
    const btnMinimize = document.getElementById('btn-minimize');
    const btnMaximize = document.getElementById('btn-maximize');
    const btnClose = document.getElementById('btn-close');
    const topBarDrag = document.getElementById('top-bar-drag');

    function sendWindowCommand(cmd) {
        if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage(JSON.stringify({ action: 'windowControl', command: cmd }));
        }
    }

    if (btnMinimize) btnMinimize.addEventListener('click', () => sendWindowCommand('minimize'));
    if (btnMaximize) btnMaximize.addEventListener('click', () => sendWindowCommand('maximize'));
    if (btnClose) btnClose.addEventListener('click', () => sendWindowCommand('close'));
    
    if (topBarDrag) {
        topBarDrag.addEventListener('mousedown', (e) => {
            if (e.button === 0) sendWindowCommand('drag');
        });
    }

    // ==========================================
    // NAVIGATION (Router)
    // ==========================================
    const navItems = document.querySelectorAll('.nav-item:not(.disabled)');
    const views = document.querySelectorAll('.view');

    navItems.forEach(item => {
        item.addEventListener('click', () => {
            navItems.forEach(nav => nav.classList.remove('active'));
            views.forEach(v => v.classList.add('hidden'));
            item.classList.add('active');
            const targetId = item.getAttribute('data-target');
            document.getElementById(targetId).classList.remove('hidden');
        });
    });

    // ==========================================
    // DOWNLOAD FAB (estilo Epic Games)
    // ==========================================
    const dlFab = document.getElementById('dl-fab');
    const dlFabBadge = document.getElementById('dl-fab-badge');
    const dlPopover = document.getElementById('dl-popover');
    const downloadsList = document.getElementById('downloads-list');
    const btnClearDownloads = document.getElementById('btn-clear-downloads');

    let activeDownloads = {}; // taskId -> { startTime, lastPercent, lastTime }
    let popoverOpen = false;

    function updateFabState() {
        const count = Object.keys(activeDownloads).length;
        if (count > 0) {
            dlFab.classList.remove('hidden');
            dlFab.classList.add('has-active');
            dlFabBadge.textContent = count;
        } else {
            dlFab.classList.add('has-active');
            dlFabBadge.textContent = '0';
            // Mantém visível por 5s após terminar
            setTimeout(() => {
                if (Object.keys(activeDownloads).length === 0) {
                    dlFab.classList.remove('has-active');
                    if (popoverOpen) return; // não ocultar se aberto
                    dlFab.classList.add('hidden');
                }
            }, 5000);
        }
    }

    if (dlFab) {
        dlFab.addEventListener('click', (e) => {
            e.stopPropagation();
            popoverOpen = !popoverOpen;
            dlPopover.classList.toggle('hidden', !popoverOpen);
        });
    }

    // Fechar popover ao clicar fora
    document.addEventListener('click', (e) => {
        if (!e.target.closest('#dl-fab-wrapper')) {
            popoverOpen = false;
            if (dlPopover) dlPopover.classList.add('hidden');
        }
    });

    if (btnClearDownloads) {
        btnClearDownloads.addEventListener('click', () => {
            // Remove apenas os concluídos
            downloadsList.querySelectorAll('.dl-card.done').forEach(el => el.remove());
            const empty = downloadsList.querySelector('.dl-empty');
            if (!empty && downloadsList.children.length === 0) {
                downloadsList.innerHTML = '<div class="dl-empty">Nenhum download em andamento.</div>';
            }
        });
    }

    function addDownloadTask(taskId, title) {
        // Remover mensagem de vazio
        const empty = downloadsList.querySelector('.dl-empty');
        if (empty) empty.remove();

        activeDownloads[taskId] = { startTime: Date.now(), lastPercent: 0, lastTime: Date.now() };
        updateFabState();

        const card = document.createElement('div');
        card.id = `dl-task-${taskId}`;
        card.className = 'dl-card';
        card.innerHTML = `
            <div class="dl-card-header">
                <span class="dl-card-title" id="dl-title-${taskId}">${title}</span>
                <span class="dl-card-percent" id="dl-percent-text-${taskId}">0%</span>
            </div>
            <div class="dl-card-bar-track">
                <div class="dl-card-bar-fill" id="dl-fill-${taskId}"></div>
            </div>
            <div class="dl-card-meta">
                <span id="dl-detail-${taskId}">Iniciando...</span>
                <span id="dl-eta-${taskId}"></span>
            </div>
        `;
        downloadsList.appendChild(card);
        downloadsList.scrollTop = downloadsList.scrollHeight;
    }

    function updateDownloadTask(taskId, percent, detail) {
        const fill = document.getElementById(`dl-fill-${taskId}`);
        const percentText = document.getElementById(`dl-percent-text-${taskId}`);
        const detailText = document.getElementById(`dl-detail-${taskId}`);
        const etaText = document.getElementById(`dl-eta-${taskId}`);

        if (fill) fill.style.width = `${percent}%`;
        if (percentText) percentText.textContent = `${percent}%`;
        if (detailText) detailText.textContent = detail || '';

        // Calcular ETA aproximado
        if (etaText && activeDownloads[taskId]) {
            const dl = activeDownloads[taskId];
            const elapsed = (Date.now() - dl.startTime) / 1000;
            if (percent > 5 && elapsed > 2) {
                const rate = percent / elapsed; // % por segundo
                const remaining = (100 - percent) / rate;
                if (remaining > 0 && remaining < 3600) {
                    const mins = Math.floor(remaining / 60);
                    const secs = Math.floor(remaining % 60);
                    etaText.textContent = mins > 0 ? `~${mins}m ${secs}s` : `~${secs}s`;
                }
            }
            dl.lastPercent = percent;
        }

        // Atualizar barra de progresso inline (lançamento)
        if (taskId === 'system') {
            const launchFill = document.getElementById('launch-progress-fill');
            const launchText = document.getElementById('launch-progress-text');
            if (launchFill) launchFill.style.width = `${percent}%`;
            if (launchText) launchText.textContent = detail || 'Processando...';
        }
    }

    function finishDownloadTask(taskId, success, msg) {
        delete activeDownloads[taskId];
        updateFabState();

        const card = document.getElementById(`dl-task-${taskId}`);
        const fill = document.getElementById(`dl-fill-${taskId}`);
        const detailText = document.getElementById(`dl-detail-${taskId}`);
        const etaText = document.getElementById(`dl-eta-${taskId}`);

        if (!card) return;
        card.classList.add('done');

        if (success) {
            if (fill) { fill.style.width = '100%'; fill.style.background = 'var(--accent-green)'; }
            if (detailText) detailText.textContent = msg || 'Concluído';
            if (etaText) etaText.textContent = '✓ OK';
            setTimeout(() => card.remove(), 8000);
        } else {
            if (fill) fill.style.background = 'var(--accent-red)';
            if (detailText) { detailText.textContent = msg || 'Erro'; detailText.style.color = 'var(--accent-red)'; }
            if (etaText) etaText.textContent = '✕ Falha';
        }

        const btns = document.querySelectorAll(`[data-slug="${taskId}"]`);
        btns.forEach(btn => {
            btn.disabled = false;
            if (btn.classList.contains('btn-install-modpack') || btn.classList.contains('btn-install-mod')) {
                btn.innerText = success ? 'Instalado' : 'Instalar';
                if (success) btn.classList.add('installed');
            }
        });
    }

    
    // ==========================================
    // LAUNCHER LOGIC
    // ==========================================
    const btnPlay = document.getElementById('btn-play');
    const btnPlayText = document.getElementById('btn-play-text');
    const btnPlaySpinner = document.getElementById('btn-play-spinner');
    const launchProgressBar = document.getElementById('launch-progress-bar');
    const launchProgressFill = document.getElementById('launch-progress-fill');
    const launchProgressText = document.getElementById('launch-progress-text');

    function setPlayLoading(isLoading) {
        if (!btnPlay) return;
        if (isLoading) {
            btnPlay.classList.add('loading');
            btnPlay.disabled = true;
            if (btnPlayText) btnPlayText.textContent = 'Iniciando...';
            if (btnPlaySpinner) btnPlaySpinner.classList.remove('hidden');
            if (launchProgressBar) launchProgressBar.classList.remove('hidden');
        } else {
            btnPlay.classList.remove('loading');
            btnPlay.disabled = false;
            if (btnPlayText) btnPlayText.textContent = 'JOGAR';
            if (btnPlaySpinner) btnPlaySpinner.classList.add('hidden');
            if (launchProgressBar) launchProgressBar.classList.add('hidden');
            if (launchProgressFill) launchProgressFill.style.width = '0%';
        }
    }

    const versionSelect = document.getElementById('version-select');
    const instanceNameInput = document.getElementById('instance-name');
    const ramSlider = document.getElementById('ram-slider');
    const ramValue = document.getElementById('ram-value');
    const actionInputs = document.querySelector('.action-inputs');

    const toggleSnapshots = document.getElementById('toggle-snapshots');

    if (versionSelect) {
        versionSelect.addEventListener('change', () => {
            renderDashboard();
        });
    }

    // ==========================================
    // ACCOUNTS UI
    // ==========================================
    const accountsList = document.getElementById('accounts-list');
    const btnAddAccount = document.getElementById('btn-add-account');
    const accountModal = document.getElementById('account-modal');
    const btnCloseAccountModal = document.getElementById('btn-close-account-modal');
    const btnConfirmAccount = document.getElementById('btn-confirm-account');
    const newAccountName = document.getElementById('new-account-name');
    const btnGotoAuth = document.getElementById('btn-goto-auth');

    // Skin Viewer
    const skinCanvas = document.getElementById('skin-canvas');
    let skinViewer;

    if (skinCanvas && window.skinview3d) {
        skinViewer = new skinview3d.SkinViewer({
            canvas: skinCanvas,
            width: skinCanvas.parentElement.clientWidth || 300,
            height: skinCanvas.parentElement.clientHeight || 400,
            skin: "https://minotar.net/skin/Steve"
        });
        
        skinViewer.camera.position.z = 60;
        skinViewer.controls.enableZoom = true;
        skinViewer.controls.enablePan = false;

        new ResizeObserver(() => {
            skinViewer.width = skinCanvas.parentElement.clientWidth;
            skinViewer.height = skinCanvas.parentElement.clientHeight;
        }).observe(skinCanvas.parentElement);
        
        // Animation buttons
        const animBtns = { walk: 'btn-anim-walk', run: 'btn-anim-run', stop: 'btn-anim-stop' };
        
        document.getElementById(animBtns.walk).addEventListener('click', () => {
            document.querySelectorAll('.anim-btn').forEach(b => b.classList.remove('active'));
            document.getElementById(animBtns.walk).classList.add('active');
            skinViewer.animations.removeAll();
            skinViewer.animations.add(skinview3d.WalkingAnimation);
        });
        document.getElementById(animBtns.run).addEventListener('click', () => {
            document.querySelectorAll('.anim-btn').forEach(b => b.classList.remove('active'));
            document.getElementById(animBtns.run).classList.add('active');
            skinViewer.animations.removeAll();
            skinViewer.animations.add(skinview3d.RunningAnimation);
        });
        document.getElementById(animBtns.stop).addEventListener('click', () => {
            document.querySelectorAll('.anim-btn').forEach(b => b.classList.remove('active'));
            document.getElementById(animBtns.stop).classList.add('active');
            skinViewer.animations.removeAll();
        });
    }

    if (btnAddAccount) btnAddAccount.addEventListener('click', () => accountModal.classList.remove('hidden'));
    if (btnCloseAccountModal) btnCloseAccountModal.addEventListener('click', () => accountModal.classList.add('hidden'));

    if (btnGotoAuth) {
        btnGotoAuth.addEventListener('click', () => {
            accountModal.classList.add('hidden');
            document.querySelector('[data-target="view-login"]').click();
        });
    }

    if (btnConfirmAccount) {
        btnConfirmAccount.addEventListener('click', () => {
            const name = newAccountName.value.trim();
            if (!name) return showToast("Digite um Nickname!", "error");
            accountModal.classList.add('hidden');
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage(JSON.stringify({
                    action: 'addOfflineAccount',
                    username: name
                }));
            }
        });
    }

    // State tracking
    let currentAccounts = [];
    let currentActiveId = null;
    let systemRamMb = 8192;

    function renderAccounts(list, activeId) {
        if (!accountsList) return;
        accountsList.innerHTML = '';
        currentAccounts = list || [];
        currentActiveId = activeId;
        
        const navLogin = document.getElementById('nav-login');
        
        if (!list || list.length === 0) {
            accountsList.innerHTML = `
                <div class="empty-state">
                    <svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path><circle cx="12" cy="7" r="4"></circle></svg>
                    <p>Nenhuma conta adicionada.</p>
                </div>`;
            if (navLogin) navLogin.style.display = 'flex';
            updateTopBar(null);
            // Redirect to auth if currently on accounts view
            const accountsView = document.getElementById('view-accounts');
            if (accountsView && !accountsView.classList.contains('hidden')) {
                document.querySelector('[data-target="view-login"]').click();
            }
            return;
        }

        const activeAcc = list.find(a => a.Id === activeId);
        
        if (!activeAcc) {
            if (navLogin) navLogin.style.display = 'flex';
        } else {
            if (navLogin) navLogin.style.display = 'none';
            const loginView = document.getElementById('view-login');
            if (loginView && !loginView.classList.contains('hidden')) {
                document.querySelector('[data-target="view-dashboard"]').click();
            }
        }

        updateTopBar(activeAcc || null);
        
        if (!activeAcc) {
            if (skinViewer) skinViewer.loadSkin("https://minotar.net/skin/Steve").catch(() => {});
            const uploadContainer = document.getElementById('skin-upload-container');
            if (uploadContainer) uploadContainer.style.display = 'none';
        }

        list.forEach(acc => {
            const isActive = acc.Id === activeId;
            const headUrl = `https://minotar.net/avatar/${acc.Username}/64`;
            const skinUrl = `https://minotar.net/skin/${acc.Username}`;
            const isApi = acc.Type === "StrafeAPI";
            
            const div = document.createElement('div');
            div.className = `account-card${isActive ? ' account-card--active' : ''}`;
            
            div.innerHTML = `
                <img src="${headUrl}" class="account-avatar" alt="${acc.Username}">
                <div class="account-info">
                    <h3 class="account-name">${acc.Username}</h3>
                    <span class="account-type ${isApi ? 'strafe-api' : 'offline'}">${isApi ? 'BR Launcher' : 'Offline'}</span>
                </div>
                <button class="btn-delete-account" data-id="${acc.Id}" title="Remover">
                    <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>
                </button>
            `;
            
            div.addEventListener('click', (e) => {
                if (e.target.closest('.btn-delete-account')) return;
                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage(JSON.stringify({ action: 'setActiveAccount', id: acc.Id }));
                }
            });
            
            accountsList.appendChild(div);

            if (isActive) {
                if (skinViewer) {
                    if (isApi) {
                        const vercelSkin = `https://brlaucher-api.vercel.app/api/skin/procurar/${acc.Username}`;
                        skinViewer.loadSkin(vercelSkin).catch(() => {
                            skinViewer.loadSkin(skinUrl).catch(() => {});
                        });
                    } else {
                        skinViewer.loadSkin(skinUrl).catch(() => {});
                    }
                }
                
                const uploadContainer = document.getElementById('skin-upload-container');
                if (uploadContainer) {
                    if (isApi) {
                        uploadContainer.style.display = 'flex';
                        uploadContainer.dataset.nick = acc.Username;
                    } else {
                        uploadContainer.style.display = 'none';
                    }
                }
            }
        });

        document.querySelectorAll('.btn-delete-account').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const id = e.currentTarget.getAttribute('data-id');
                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage(JSON.stringify({ action: 'deleteAccount', id: id }));
                }
            });
        });

        // Update dashboard
        renderDashboard();
    }

    function updateTopBar(acc) {
        const topbar = document.getElementById('topbar-account');
        const dashAvatar = document.getElementById('dashboard-avatar');
        const dashWelcome = document.getElementById('dashboard-welcome');
        
        if (acc) {
            if (topbar) {
                const headUrl = `https://minotar.net/avatar/${acc.Username}/24`;
                topbar.innerHTML = `
                    <img src="${headUrl}" style="width:20px; height:20px; border-radius:4px; image-rendering: pixelated;"> 
                    <span style="color:white; font-weight:600;">${acc.Username}</span>
                    <button id="btn-logout" title="Deslogar" style="background:none; border:none; color:var(--accent-red); cursor:pointer; margin-left: 8px; display:flex; align-items:center; transition:0.2s;">
                        <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"></path><polyline points="16 17 21 12 16 7"></polyline><line x1="21" y1="12" x2="9" y2="12"></line></svg>
                    </button>
                `;
            }
            
            if (dashAvatar) {
                dashAvatar.style.width = "70px";
                dashAvatar.style.height = "70px";
                dashAvatar.style.borderRadius = "0px";
                dashAvatar.style.boxShadow = "none";
                dashAvatar.style.filter = "none";
                dashAvatar.src = `https://mc-heads.net/head/${acc.Username}/70`;
            }
            if (dashWelcome) {
                dashWelcome.innerText = `Bem-vindo, ${acc.Username}!`;
            }
            
            const btnLogout = document.getElementById('btn-logout');
            btnLogout.addEventListener('click', (e) => {
                e.stopPropagation();
                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage(JSON.stringify({ action: 'logout' }));
                }
            });
            
            btnLogout.addEventListener('mouseenter', () => btnLogout.style.transform = 'scale(1.1)');
            btnLogout.addEventListener('mouseleave', () => btnLogout.style.transform = 'scale(1)');
        } else {
            topbar.innerHTML = `<span class="slot-status disconnected"></span> Sem Conta`;
        }
    }

    // ==========================================
    // DASHBOARD DYNAMIC CARDS
    // ==========================================
    function renderDashboard() {
        const container = document.getElementById('dashboard-stats');
        if (!container) return;

        const activeAcc = currentAccounts.find(a => a.Id === currentActiveId);
        const accCount = currentAccounts.length;
        const headUrl = activeAcc ? `https://minotar.net/avatar/${activeAcc.Username}/64` : null;

        container.innerHTML = '';

        // Card: Conta Ativa
        const accCard = document.createElement('div');
        accCard.className = 'dash-card clickable';
        accCard.innerHTML = `
            <div class="dash-card-header">
                <div class="dash-card-icon blue">
                    <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"></path><circle cx="12" cy="7" r="4"></circle></svg>
                </div>
                <div>
                    <div class="dash-card-title">Conta Ativa</div>
                    <div class="dash-card-value">${activeAcc ? activeAcc.Username : 'Nenhuma'}</div>
                </div>
            </div>
            <div class="dash-card-sub">${accCount} conta${accCount !== 1 ? 's' : ''} cadastrada${accCount !== 1 ? 's' : ''}</div>
        `;
        accCard.addEventListener('click', () => document.querySelector('[data-target="view-accounts"]').click());
        container.appendChild(accCard);

        // Card: Sistema
        const sysCard = document.createElement('div');
        sysCard.className = 'dash-card';
        const ramGb = (systemRamMb / 1024).toFixed(1);
        sysCard.innerHTML = `
            <div class="dash-card-header">
                <div class="dash-card-icon green">
                    <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><rect x="4" y="4" width="16" height="16" rx="2"></rect><rect x="9" y="9" width="6" height="6"></rect><line x1="9" y1="1" x2="9" y2="4"></line><line x1="15" y1="1" x2="15" y2="4"></line><line x1="9" y1="20" x2="9" y2="23"></line><line x1="15" y1="20" x2="15" y2="23"></line><line x1="20" y1="9" x2="23" y2="9"></line><line x1="20" y1="14" x2="23" y2="14"></line><line x1="1" y1="9" x2="4" y2="9"></line><line x1="1" y1="14" x2="4" y2="14"></line></svg>
                </div>
                <div>
                    <div class="dash-card-title">Memória RAM</div>
                    <div class="dash-card-value">${ramGb} GB</div>
                </div>
            </div>
            <div class="dash-card-sub">Alocando ${ramSlider.value} MB para o jogo</div>
        `;
        container.appendChild(sysCard);

        // Card: Versão
        const verCard = document.createElement('div');
        verCard.className = 'dash-card';
        const selectedVer = versionSelect.value || 'Carregando...';
        verCard.innerHTML = `
            <div class="dash-card-header">
                <div class="dash-card-icon purple">
                    <svg xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"></path></svg>
                </div>
                <div>
                    <div class="dash-card-title">Versão Selecionada</div>
                    <div class="dash-card-value">${selectedVer}</div>
                </div>
            </div>
            <div class="dash-card-sub">Pronto para jogar</div>
        `;
        container.appendChild(verCard);
    }

    // ==========================================
    // INSTANCES UI
    // ==========================================
    const instancesGrid = document.getElementById('instances-grid');
    const btnNewInstance = document.getElementById('btn-new-instance');
    const createInstanceModal = document.getElementById('instance-modal');
    const btnCloseCreateInstance = document.getElementById('btn-close-instance-modal');
    const btnConfirmCreate = document.getElementById('btn-confirm-instance');
    const newInstanceName = document.getElementById('new-instance-name');
    const newInstanceVersion = document.getElementById('new-instance-version');
    const newInstanceModloader = document.getElementById('new-instance-modloader');
    const editOldName = document.getElementById('edit-old-name');
    const instanceModalTitle = document.getElementById('instance-modal-title');

    // Update UI
    const updateModal = document.getElementById('update-modal');
    const updateNotes = document.getElementById('update-notes');
    const btnCloseUpdate = document.getElementById('btn-close-update');

    // Local Mods UI
    const localModsModal = document.getElementById('local-mods-modal');
    const btnCloseLocalMods = document.getElementById('btn-close-local-mods');
    const btnImportLocal = document.getElementById('btn-import-local'); // Mod Builder
    const btnImportLocalManual = document.getElementById('btn-import-local-manual'); // Manual Instance
    const manualLocalModsCount = document.getElementById('manual-local-mods-count');
    const btnSelectAllLocalMods = document.getElementById('btn-select-all-local-mods');
    const localModsCount = document.getElementById('local-mods-count');
    const localModsList = document.getElementById('local-mods-list');
    const btnConfirmLocalMods = document.getElementById('btn-confirm-local-mods');
    
    let localModsData = [];
    let localModsCaller = 'builder'; // 'builder' or 'manual'
    let manualInstanceLocalMods = [];

    let allVersions = [];

    // Recupera configurações do localStorage
    const savedInstance = localStorage.getItem('last_instance');
    if (savedInstance && instanceNameInput) instanceNameInput.value = savedInstance;
    const savedRam = localStorage.getItem('last_ram');
    if (savedRam) {
        ramSlider.value = savedRam;
        ramValue.innerText = savedRam + ' MB';
    }
    const showSnapshots = localStorage.getItem('show_snapshots') === 'true';
    if (toggleSnapshots) toggleSnapshots.checked = showSnapshots;

    if (toggleSnapshots) {
        toggleSnapshots.addEventListener('change', (e) => {
            localStorage.setItem('show_snapshots', e.target.checked);
            renderVersions();
        });
    }

    ramSlider.addEventListener('input', (e) => {
        ramValue.innerText = e.target.value + ' MB';
        renderDashboard();
    });

    function renderVersions() {
        versionSelect.innerHTML = '';
        newInstanceVersion.innerHTML = '';
        const showSnaps = toggleSnapshots ? toggleSnapshots.checked : false;

        const releases = allVersions.filter(v => v.Type === 'release' || v.Type === 'Release');
        const snapshots = allVersions.filter(v => v.Type === 'snapshot' || v.Type === 'Snapshot');
        // Versões que são locais ou que não se encaixam em release/snapshot
        const locals = allVersions.filter(v => v.IsLocal || (v.Type !== 'release' && v.Type !== 'Release' && v.Type !== 'snapshot' && v.Type !== 'Snapshot'));

        if (locals.length > 0) {
            const localGroup = document.createElement('optgroup');
            localGroup.label = 'Instaladas / Customizadas';
            locals.forEach(v => {
                const opt = document.createElement('option');
                opt.value = v.Name;
                opt.textContent = v.Name;
                localGroup.appendChild(opt);
            });
            versionSelect.appendChild(localGroup.cloneNode(true));
            newInstanceVersion.appendChild(localGroup.cloneNode(true));
        }

        const releaseGroup = document.createElement('optgroup');
        releaseGroup.label = 'Releases';
        releases.forEach(v => {
            const opt = document.createElement('option');
            opt.value = v.Name;
            opt.textContent = v.Name;
            releaseGroup.appendChild(opt);
        });
        versionSelect.appendChild(releaseGroup.cloneNode(true));
        newInstanceVersion.appendChild(releaseGroup.cloneNode(true));
        
        const builderVersionSelect = document.getElementById('builder-version');
        if (builderVersionSelect) {
            builderVersionSelect.innerHTML = '';
            builderVersionSelect.appendChild(releaseGroup.cloneNode(true));
        }

        if (showSnaps && snapshots.length > 0) {
            const snapGroup = document.createElement('optgroup');
            snapGroup.label = 'Snapshots';
            snapshots.forEach(v => {
                const opt = document.createElement('option');
                opt.value = v.Name;
                opt.textContent = v.Name;
                snapGroup.appendChild(opt);
            });
            versionSelect.appendChild(snapGroup.cloneNode(true));
            newInstanceVersion.appendChild(snapGroup.cloneNode(true));
        }

        renderDashboard();
    }

    function renderInstances(list) {
        if (!instancesGrid) return;
        instancesGrid.innerHTML = '';
        if (!list || list.length === 0) {
            instancesGrid.innerHTML = `
                <div class="empty-state" style="grid-column: 1 / -1;">
                    <svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"></path></svg>
                    <p>Nenhuma instância criada.</p>
                </div>`;
            return;
        }

        list.forEach(inst => {
            const card = document.createElement('div');
            card.className = 'instance-card';
            card.style.position = 'relative';
            card.innerHTML = `
                <div class="instance-card-actions">
                    <button class="anim-btn btn-edit-instance" data-name="${inst.Name}" data-ver="${inst.MinecraftVersion}" data-mod="${inst.Modloader}" data-opt="${inst.EnableOptimization !== false}" title="Editar">
                        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M17 3a2.83 2.83 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5Z"></path></svg>
                    </button>
                    <button class="anim-btn btn-mods-instance" data-name="${inst.Name}" data-ver="${inst.MinecraftVersion}" data-mod="${inst.Modloader}" title="Mods">
                        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polygon points="12 2 2 7 12 12 22 7 12 2"></polygon><polyline points="2 17 12 22 22 17"></polyline></svg>
                    </button>
                    <button class="anim-btn btn-delete-instance" data-name="${inst.Name}" title="Deletar" style="color: var(--accent-red);">
                        <svg xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><polyline points="3 6 5 6 21 6"></polyline><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"></path></svg>
                    </button>
                </div>
                <svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="var(--accent-blue)" stroke-width="1.5"><path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"></path><polyline points="3.27 6.96 12 12.01 20.73 6.96"></polyline><line x1="12" y1="22.08" x2="12" y2="12"></line></svg>
                <h3>${inst.Name}</h3>
                <span class="badge">${inst.MinecraftVersion} | ${inst.Modloader}</span>
                <button class="btn-play-instance" data-name="${inst.Name}" data-ver="${inst.MinecraftVersion}">Jogar</button>
            `;
            instancesGrid.appendChild(card);
        });

        // Play instance
        document.querySelectorAll('.btn-play-instance').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const name = e.target.getAttribute('data-name');
                const ver = e.target.getAttribute('data-ver');
                if (instanceNameInput) instanceNameInput.value = name;
                versionSelect.value = ver;
                document.querySelector('[data-target="view-dashboard"]').click();
                btnPlay.click();
            });
        });

        // Mods instance
        document.querySelectorAll('.btn-mods-instance').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const el = e.currentTarget;
                window.currentModManagerInstance = {
                    name: el.getAttribute('data-name'),
                    ver: el.getAttribute('data-ver'),
                    modloader: el.getAttribute('data-mod')
                };
                document.getElementById('instance-mods-target').innerText = el.getAttribute('data-name');
                document.getElementById('instance-mods-results-grid').innerHTML = '';
                views.forEach(v => v.classList.add('hidden'));
                document.getElementById('view-instance-mods').classList.remove('hidden');
            });
        });

        // Delete instance
        document.querySelectorAll('.btn-delete-instance').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const name = e.currentTarget.getAttribute('data-name');
                if (confirm(`Deletar o modpack '${name}'? Essa ação é irreversível!`)) {
                    if (window.chrome && window.chrome.webview) {
                        window.chrome.webview.postMessage(JSON.stringify({ action: 'deleteInstance', name: name }));
                    }
                }
            });
        });

        // Edit instance
        document.querySelectorAll('.btn-edit-instance').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const el = e.currentTarget;
                instanceModalTitle.innerText = "Editar Instância";
                editOldName.value = el.getAttribute('data-name');
                newInstanceName.value = el.getAttribute('data-name');
                if (Array.from(newInstanceVersion.options).some(o => o.value === el.getAttribute('data-ver'))) {
                    newInstanceVersion.value = el.getAttribute('data-ver');
                }
                newInstanceModloader.value = el.getAttribute('data-mod') || "None";
                const enableOpt = el.getAttribute('data-opt') !== 'false';
                const enableOptChk = document.getElementById('new-instance-enable-opt');
                if (enableOptChk) enableOptChk.checked = enableOpt;
                createInstanceModal.classList.remove('hidden');
            });
        });
    }

    // Modal Handlers
    btnNewInstance.addEventListener('click', () => {
        instanceModalTitle.innerText = "Criar Instância";
        editOldName.value = "";
        newInstanceName.value = "";
        newInstanceModloader.value = "None";
        const enableOptChk = document.getElementById('new-instance-enable-opt');
        if (enableOptChk) enableOptChk.checked = true;
        createInstanceModal.classList.remove('hidden');
    });
    btnCloseCreateInstance.addEventListener('click', () => createInstanceModal.classList.add('hidden'));
    
    btnConfirmCreate.addEventListener('click', () => {
        const oldName = editOldName.value;
        let name = newInstanceName.value.trim();
        const ver = newInstanceVersion.value;
        const modloader = newInstanceModloader.value;

        // Remover caracteres inválidos para pastas do Windows
        name = name.replace(/[<>:"/\\|?*]/g, '');

        if(!name || !ver) return showToast("Preencha os campos com um nome válido!", "error");

        createInstanceModal.classList.add('hidden');
        
        if (window.chrome && window.chrome.webview) {
            const syncWorlds = document.getElementById('new-instance-sync-worlds') ? document.getElementById('new-instance-sync-worlds').checked : true;
            const enableOpt = document.getElementById('new-instance-enable-opt') ? document.getElementById('new-instance-enable-opt').checked : true;

            if (oldName) {
                window.chrome.webview.postMessage(JSON.stringify({
                    action: 'editInstance',
                    oldName: oldName,
                    info: { Name: name, MinecraftVersion: ver, Modloader: modloader, SyncVanillaWorlds: syncWorlds, EnableOptimization: enableOpt }
                }));
            } else {
                window.chrome.webview.postMessage(JSON.stringify({
                    action: 'createInstance',
                    info: { Name: name, MinecraftVersion: ver, Modloader: modloader, SyncVanillaWorlds: syncWorlds, EnableOptimization: enableOpt },
                    localMods: manualInstanceLocalMods
                }));
            }
            // reset
            manualInstanceLocalMods = [];
            if (manualLocalModsCount) manualLocalModsCount.innerText = '0 selecionados';
        }
    });

    if (btnPlay) {
        btnPlay.addEventListener('click', () => {
            const ver = versionSelect.value;
            const ram = parseInt(ramSlider.value);
            let inst = (instanceNameInput && instanceNameInput.value.trim() !== '') ? instanceNameInput.value.trim() : 'padrao';
            
            inst = inst.replace(/[<>:"/\\|?*]/g, '');
            if(!inst) inst = 'padrao';
            
            if(!ver) return showToast('Selecione uma versão!', 'error');

            localStorage.setItem('last_instance', inst);
            localStorage.setItem('last_ram', ram);

            setPlayLoading(true);
            addDownloadTask('system', `Minecraft ${ver}`);

            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage(JSON.stringify({ 
                    action: 'play', 
                    version: ver,
                    ramMb: ram,
                    instanceName: inst
                }));
            }
        });
    }

    // Modpacks search
    const searchModInput = document.getElementById('search-mod-input');
    const btnSearchMods = document.getElementById('btn-search-mods');
    if (btnSearchMods) {
        btnSearchMods.addEventListener('click', () => {
            const query = searchModInput.value.trim();
            if (!query) return;
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage(JSON.stringify({ action: 'searchModpacks', query: query }));
            }
        });
    }

    // Instance mods search
    const searchInstanceModInput = document.getElementById('search-instance-mod-input');
    const btnSearchInstanceMods = document.getElementById('btn-search-instance-mods');
    if (btnSearchInstanceMods) {
        btnSearchInstanceMods.addEventListener('click', () => {
            const query = searchInstanceModInput.value.trim();
            if (!query || !window.currentModManagerInstance) return;
            const { ver, modloader } = window.currentModManagerInstance;
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage(JSON.stringify({ 
                    action: 'searchMods', 
                    query: query,
                    version: ver,
                    modloader: modloader
                }));
            }
        });
    }

    // Open Instance Mods Folder
    const btnOpenInstanceFolder = document.getElementById('btn-open-instance-folder');
    if (btnOpenInstanceFolder) {
        btnOpenInstanceFolder.addEventListener('click', () => {
            if (window.currentModManagerInstance && window.currentModManagerInstance.name) {
                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage(JSON.stringify({
                        action: 'openInstanceFolder',
                        name: window.currentModManagerInstance.name
                    }));
                }
            }
        });
    }

    // ==========================================
    // MOD BUILDER LOGIC
    // ==========================================
    window.modpackBuilderState = {
        cart: [] // { id, name, icon, versionId }
    };

    const builderVersionSelect = document.getElementById('builder-version');
    const builderModloaderSelect = document.getElementById('builder-modloader');
    const btnBuilderSearch = document.getElementById('btn-builder-search');
    const builderSearchInput = document.getElementById('builder-search');
    const builderResults = document.getElementById('builder-results');
    const builderCart = document.getElementById('builder-cart');
    const builderCount = document.getElementById('builder-count');
    const btnBuilderCreate = document.getElementById('btn-builder-create');
    const builderInstanceName = document.getElementById('builder-instance-name');

    if (btnBuilderSearch) {
        btnBuilderSearch.addEventListener('click', () => {
            const query = builderSearchInput.value.trim();
            const ver = builderVersionSelect.value;
            const modloader = builderModloaderSelect.value;
            if (!query || !ver) return;

            builderResults.innerHTML = '<div class="empty-state"><p>Buscando...</p></div>';
            
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage(JSON.stringify({ 
                    action: 'searchBuilderMods', 
                    query: query,
                    version: ver,
                    modloader: modloader
                }));
            }
        });
    }

    window.addModToBuilderCart = function(projectId, name, iconUrl) {
        // Verifica se já tem
        if (window.modpackBuilderState.cart.find(m => m.id === projectId)) {
            return showToast('Mod já adicionado!', 'error');
        }
        
        window.modpackBuilderState.cart.push({ id: projectId, name: name, icon: iconUrl });
        renderBuilderCart();
        showToast(`${name} adicionado!`, 'success');
        
        // Travar selects para evitar incompatibilidade
        builderVersionSelect.disabled = true;
        builderModloaderSelect.disabled = true;
    };

    window.removeModFromBuilderCart = function(projectId) {
        window.modpackBuilderState.cart = window.modpackBuilderState.cart.filter(m => m.id !== projectId);
        renderBuilderCart();
        
        // Destravar se vazio
        if (window.modpackBuilderState.cart.length === 0) {
            builderVersionSelect.disabled = false;
            builderModloaderSelect.disabled = false;
        }
    };

    function renderBuilderCart() {
        if (!builderCart) return;
        builderCart.innerHTML = '';
        
        const cart = window.modpackBuilderState.cart;
        builderCount.innerText = `${cart.length} Mods`;

        if (cart.length === 0) {
            builderCart.innerHTML = '<div class="empty-state"><p>Nenhum mod selecionado.</p></div>';
            return;
        }

        cart.forEach(mod => {
            const div = document.createElement('div');
            div.style.cssText = 'display:flex; align-items:center; gap:10px; padding:10px; background:rgba(255,255,255,0.05); border-radius:8px;';
            div.innerHTML = `
                <img src="${mod.icon || 'https://via.placeholder.com/32'}" style="width:32px; height:32px; border-radius:4px;">
                <span style="flex:1; font-weight:600; font-size:0.9rem; white-space:nowrap; overflow:hidden; text-overflow:ellipsis;">
                    ${mod.type === 'local' ? '<span style="color:var(--accent-purple); margin-right:5px;">[Local]</span>' : ''}${mod.name}
                </span>
                <button onclick="window.removeModFromBuilderCart('${mod.id.replace(/\\/g, '\\\\')}')" style="background:none; border:none; color:var(--accent-red); cursor:pointer;">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><line x1="18" y1="6" x2="6" y2="18"></line><line x1="6" y1="6" x2="18" y2="18"></line></svg>
                </button>
            `;
            builderCart.appendChild(div);
        });
    }

    if (btnBuilderCreate) {
        btnBuilderCreate.addEventListener('click', () => {
            const name = builderInstanceName.value.trim();
            const ver = builderVersionSelect.value;
            const modloader = builderModloaderSelect.value;
            const mods = window.modpackBuilderState.cart.map(m => m.id);

            if (!name) return showToast('Dê um nome para o seu Modpack!', 'error');
            if (mods.length === 0) return showToast('Selecione pelo menos um mod!', 'error');

            const cleanName = name.replace(/[<>:"/\\|?*]/g, '');

            addDownloadTask('builder', `Gerando ${cleanName}...`);
            showToast('Iniciando montagem do Modpack...', 'info');

            // Reset UI for next build
            window.modpackBuilderState.cart = [];
            renderBuilderCart();
            builderInstanceName.value = '';
            builderVersionSelect.disabled = false;
            builderModloaderSelect.disabled = false;
            document.querySelector('[data-target="view-instances"]').click();

            if (window.chrome && window.chrome.webview) {
                const syncWorlds = document.getElementById('builder-sync-worlds') ? document.getElementById('builder-sync-worlds').checked : true;
                
                const modrinthMods = window.modpackBuilderState.cart.filter(m => m.type !== 'local').map(m => m.id);
                const localModsPaths = window.modpackBuilderState.cart.filter(m => m.type === 'local').map(m => m.id);

                window.chrome.webview.postMessage(JSON.stringify({
                    action: 'buildModpack',
                    name: cleanName,
                    version: ver,
                    modloader: modloader,
                    syncWorlds: syncWorlds,
                    mods: modrinthMods,
                    localMods: localModsPaths
                }));
            }
        });
    }

    // Back to instances
    const btnBackToInstances = document.getElementById('btn-back-to-instances');
    if (btnBackToInstances) {
        btnBackToInstances.addEventListener('click', () => {
            document.querySelector('[data-target="view-instances"]').click();
        });
    }

    // Update modal
    if (btnCloseUpdate) btnCloseUpdate.addEventListener('click', () => updateModal.classList.add('hidden'));

    // Local Mods Modal Logic
    if (btnImportLocal) {
        btnImportLocal.addEventListener('click', () => {
            localModsCaller = 'builder';
            if (localModsModal) localModsModal.classList.remove('hidden');
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage(JSON.stringify({ action: 'getLocalMods' }));
            }
        });
    }

    if (btnImportLocalManual) {
        btnImportLocalManual.addEventListener('click', () => {
            localModsCaller = 'manual';
            if (localModsModal) localModsModal.classList.remove('hidden');
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage(JSON.stringify({ action: 'getLocalMods' }));
            }
        });
    }

    if (btnSelectAllLocalMods) {
        btnSelectAllLocalMods.addEventListener('click', () => {
            const checkboxes = localModsList.querySelectorAll('input[type="checkbox"]');
            const allChecked = Array.from(checkboxes).every(cb => cb.checked);
            checkboxes.forEach(cb => cb.checked = !allChecked);
        });
    }

    if (btnCloseLocalMods) btnCloseLocalMods.addEventListener('click', () => localModsModal.classList.add('hidden'));

    if (btnConfirmLocalMods) {
        btnConfirmLocalMods.addEventListener('click', () => {
            const checkboxes = localModsList.querySelectorAll('input[type="checkbox"]:checked');
            let addedCount = 0;
            
            if (localModsCaller === 'builder') {
                checkboxes.forEach(cb => {
                    const idx = parseInt(cb.value);
                    const mod = localModsData[idx];
                    if (mod && !window.modpackBuilderState.cart.find(m => m.id === mod.path)) {
                        window.modpackBuilderState.cart.push({
                            id: mod.path,
                            name: mod.filename,
                            icon: 'https://via.placeholder.com/32/4a148c/FFFFFF?text=L',
                            type: 'local'
                        });
                        addedCount++;
                    }
                });
                renderBuilderCart();
                if (addedCount > 0) showToast(`${addedCount} mods locais importados pro carrinho!`, 'success');
            } else {
                checkboxes.forEach(cb => {
                    const idx = parseInt(cb.value);
                    const mod = localModsData[idx];
                    if (mod && !manualInstanceLocalMods.includes(mod.path)) {
                        manualInstanceLocalMods.push(mod.path);
                        addedCount++;
                    }
                });
                if (manualLocalModsCount) manualLocalModsCount.innerText = `${manualInstanceLocalMods.length} selecionados`;
                if (addedCount > 0) showToast(`${addedCount} mods locais selecionados!`, 'success');
            }
            
            localModsModal.classList.add('hidden');
        });
    }

    function renderLocalMods(mods) {
        localModsData = mods;
        if (localModsCount) localModsCount.innerText = `${mods.length} mods encontrados`;
        if (!localModsList) return;
        localModsList.innerHTML = '';
        if (mods.length === 0) {
            localModsList.innerHTML = '<div style="text-align:center; padding:20px; color:var(--text-secondary);">Nenhum mod local (.jar) encontrado em .minecraft/mods.</div>';
            return;
        }

        mods.forEach((mod, index) => {
            const div = document.createElement('div');
            div.style.cssText = 'display:flex; align-items:center; gap:10px; padding:10px; background:rgba(255,255,255,0.05); border-radius:6px; cursor:pointer;';
            div.innerHTML = `
                <input type="checkbox" id="local-mod-${index}" value="${index}" style="accent-color:var(--accent-purple); width:18px; height:18px; cursor:pointer;" />
                <label for="local-mod-${index}" style="flex:1; cursor:pointer; font-size:0.9rem; word-break:break-all;">${mod.filename}</label>
            `;
            localModsList.appendChild(div);
        });
    }

    // ==========================================
    // WEBVIEW2 MESSAGING
    // ==========================================
    if (window.chrome && window.chrome.webview) {
        window.chrome.webview.addEventListener('message', event => {
            try {
                const data = JSON.parse(event.data);
                if (data.type === 'versions') {
                    allVersions = data.list;
                    renderVersions();
                }
                else if (data.type === 'deviceCode') {
                    const modal = document.getElementById('device-code-modal');
                    const codeDisplay = document.getElementById('device-code-display');
                    const btnOpen = document.getElementById('btn-open-ms-link');
                    const btnCancel = document.getElementById('btn-cancel-ms-login');
                    
                    codeDisplay.innerText = data.code;
                    modal.classList.remove('hidden');

                    btnOpen.onclick = () => {
                        navigator.clipboard.writeText(data.code).then(() => {
                            showToast('Código copiado!', 'success');
                            window.chrome.webview.postMessage(JSON.stringify({ action: 'openUrl', url: data.url }));
                        });
                    };

                    btnCancel.onclick = () => {
                        modal.classList.add('hidden');
                        // In a real app we might also send a cancel signal back
                    };
                }
                else if (data.type === 'hideDeviceCode') {
                    document.getElementById('device-code-modal').classList.add('hidden');
                }
                else if (data.type === 'microsoftLoginSuccess') {
                    showToast('Logado com sucesso como: ' + data.nick, 'success');
                    document.getElementById('device-code-modal').classList.add('hidden');
                    
                    // Vai para a tela principal
                    document.querySelectorAll('.nav-item').forEach(b => b.classList.remove('active'));
                    document.querySelectorAll('.view').forEach(v => v.classList.remove('active'));
                    document.querySelector('[data-target="view-dashboard"]').classList.add('active');
                    document.getElementById('view-dashboard').classList.add('active');
                }
                else if (data.type === 'microsoftLoginError') {
                    showToast('Erro no login Microsoft: ' + data.message, 'error');
                    document.getElementById('device-code-modal').classList.add('hidden');
                }
                else if (data.type === 'localMods') {
                    renderLocalMods(data.list);
                }
                else if (data.type === 'systemInfo') {
                    systemRamMb = data.totalRamMb;
                    ramSlider.max = data.totalRamMb;
                    if (parseInt(ramSlider.value) > data.totalRamMb) {
                        ramSlider.value = data.totalRamMb;
                        ramValue.innerText = data.totalRamMb + ' MB';
                    }
                    renderDashboard();
                }
                else if (data.type === 'status') {
                    updateDownloadTask(data.taskId || 'system', 50, data.text);
                    if (data.resetUI) {
                        setPlayLoading(false);
                        finishDownloadTask(data.taskId || 'system', true, data.text);
                    }
                }
                else if (data.type === 'progress') {
                    updateDownloadTask(data.taskId || 'system', data.percent, data.detail);
                }
                else if (data.type === 'downloadSuccess') {
                    finishDownloadTask(data.taskId, true, data.text);
                    showToast(data.text, 'success');
                }
                else if (data.type === 'downloadError') {
                    finishDownloadTask(data.taskId, false, data.text);
                    showToast(data.text, 'error');
                }
                else if (data.type === 'error') {
                    showToast(data.text, 'error');
                    setPlayLoading(false);
                    finishDownloadTask('system', false, data.text);
                }
                else if (data.type === 'instances') {
                    renderInstances(data.list);
                }
                else if (data.type === 'accounts') {
                    renderAccounts(data.list, data.activeId);
                }
                else if (data.type === 'updateStatus' && data.hasUpdate) {
                    updateNotes.innerText = data.notes;
                    updateModal.classList.remove('hidden');
                }
                else if (data.type === 'modpackResults') {
                    renderModResults('mods-results-grid', data.results, 'modpack');
                }
                else if (data.type === 'modResults') {
                    renderModResults('instance-mods-results-grid', data.results, 'mod');
                }
                else if (data.type === 'builderModResults') {
                    renderModResults('builder-results', data.results, 'builder');
                }
            } catch (e) { console.error("Erro", e); }
        });

        // Initial requests
        window.chrome.webview.postMessage(JSON.stringify({ action: 'getAccounts' }));
        window.chrome.webview.postMessage(JSON.stringify({ action: 'getVersions' }));
        window.chrome.webview.postMessage(JSON.stringify({ action: 'getSystemInfo' }));
        window.chrome.webview.postMessage(JSON.stringify({ action: 'getInstances' }));
        window.chrome.webview.postMessage(JSON.stringify({ action: 'checkForUpdates' }));
    }

    // ==========================================
    // MOD RESULTS RENDERER (Shared)
    // ==========================================
    function renderModResults(gridId, results, type) {
        const grid = document.getElementById(gridId);
        grid.innerHTML = '';
        if (!results || results.length === 0) {
            grid.innerHTML = `
                <div class="empty-state">
                    <svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="11" cy="11" r="8"></circle><line x1="21" y1="21" x2="16.65" y2="16.65"></line></svg>
                    <p>Nenhum resultado encontrado.</p>
                </div>`;
            return;
        }

        results.forEach(item => {
            const card = document.createElement('div');
            card.className = 'mod-result-card';
            const btnText = type === 'builder' ? 'Adicionar' : 'Instalar';
            card.innerHTML = `
                <img src="${item.icon_url || 'https://via.placeholder.com/56'}" alt="${item.title}">
                <div class="mod-result-info">
                    <h3>${item.title}</h3>
                    <p>${item.description}</p>
                </div>
                <button class="btn-install btn-install-${type}" data-slug="${item.slug}" data-project="${item.project_id}" data-title="${item.title}" data-icon="${item.icon_url || ''}">${btnText}</button>
            `;
            grid.appendChild(card);
        });

        grid.querySelectorAll(`.btn-install-${type}`).forEach(btn => {
            btn.addEventListener('click', (e) => {
                const slug = e.target.getAttribute('data-slug');
                const proj = e.target.getAttribute('data-project');
                const title = e.target.getAttribute('data-title');
                const icon = e.target.getAttribute('data-icon');
                
                if (type === 'builder') {
                    window.addModToBuilderCart(proj, title, icon);
                    e.target.innerText = "Adicionado";
                    e.target.disabled = true;
                    return;
                }
                
                e.target.innerText = "Adicionado";
                e.target.disabled = true;
                
                addDownloadTask(slug, `${type === 'modpack' ? 'Pack' : 'Mod'}: ${slug}`);
                
                if (type === 'modpack') {
                    window.chrome.webview.postMessage(JSON.stringify({
                        action: 'installModpack', slug, projectId: proj
                    }));
                } else {
                    const { name, ver, modloader } = window.currentModManagerInstance;
                    window.chrome.webview.postMessage(JSON.stringify({
                        action: 'installMod', slug, projectId: proj,
                        version: ver, modloader, instanceName: name
                    }));
                }
            });
        });
    }

    // ==========================================
    // AUTHENTICATION LOGIC
    // ==========================================
    const tabLogin = document.getElementById('tab-login');
    const tabRegister = document.getElementById('tab-register');
    const formLogin = document.getElementById('form-login');
    const formRegister = document.getElementById('form-register');

    if (tabLogin && tabRegister) {
        tabLogin.addEventListener('click', () => {
            tabLogin.classList.add('active');
            tabRegister.classList.remove('active');
            formLogin.classList.add('active');
            formRegister.classList.remove('active');
        });
        tabRegister.addEventListener('click', () => {
            tabRegister.classList.add('active');
            tabLogin.classList.remove('active');
            formRegister.classList.add('active');
            formLogin.classList.remove('active');
        });
    }

    // Register
    if (formRegister) {
        formRegister.addEventListener('submit', async (e) => {
            e.preventDefault();
            const btn = formRegister.querySelector('button');
            const originalText = btn.innerHTML;
            btn.innerHTML = "Cadastrando...";
            btn.disabled = true;

            const nick = document.getElementById('reg-nick').value.trim();
            const email = document.getElementById('reg-email').value.trim();
            const pass = document.getElementById('reg-pass').value;

            try {
                const response = await fetch('https://brlaucher-api.vercel.app/api/auth/registrar', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ email, nick, senha: pass })
                });

                const data = await response.json().catch(() => ({}));

                if (response.ok) {
                    // Registro bem-sucedido — aguarda confirmação de e-mail
                    showEmailVerificationBanner(email);

                    // Limpa o formulário e vai para a aba de login
                    formRegister.reset();
                    if (tabLogin) tabLogin.click();
                } else if (response.status === 429 || data.rateLimitado) {
                    // Rate limit do Supabase — muitos e-mails enviados
                    showToast('Muitas tentativas. Aguarde alguns minutos e tente novamente.', 'error');
                } else {
                    showToast(data.mensagem || data.error || "Verifique os dados enviados.", 'error');
                }
            } catch (error) {
                showToast("Erro de conexão com a API: " + error.message, 'error');
            } finally {
                btn.innerHTML = originalText;
                btn.disabled = false;
            }
        });
    }

    // Login
    if (formLogin) {
        formLogin.addEventListener('submit', async (e) => {
            e.preventDefault();
            const btn = formLogin.querySelector('button');
            const originalText = btn.innerHTML;
            btn.innerHTML = "Entrando...";
            btn.disabled = true;

            const email = document.getElementById('login-email').value.trim();
            const pass = document.getElementById('login-pass').value;

            try {
                const response = await fetch('https://brlaucher-api.vercel.app/api/auth/login', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ email, senha: pass })
                });

                const data = await response.json().catch(() => ({}));

                if (response.ok) {
                    const nick = data.nick || email.split('@')[0];
                    if (window.chrome && window.chrome.webview) {
                        window.chrome.webview.postMessage(JSON.stringify({
                            action: 'LOGIN_SUCESSO', nick, token: data.token || ''
                        }));
                    }
                    document.querySelector('[data-target="view-dashboard"]').click();
                    showToast(`Logado como ${nick}!`, 'success');
                } else if (response.status === 403 && data.emailPendente) {
                    // E-mail ainda não confirmado — mostra banner especial com opção de reenviar
                    showEmailVerificationBanner(email);
                    if (tabLogin) tabLogin.click();
                } else {
                    showToast(data.mensagem || data.error || "E-mail ou senha incorretos.", 'error');
                }
            } catch (error) {
                showToast("Erro de conexão com a API: " + error.message, 'error');
            } finally {
                btn.innerHTML = originalText;
                btn.disabled = false;
            }
        });
    }

    // Microsoft Login
    const btnLoginMicrosoft = document.getElementById('btn-login-microsoft');
    if (btnLoginMicrosoft) {
        btnLoginMicrosoft.addEventListener('click', () => {
            const originalText = btnLoginMicrosoft.innerHTML;
            btnLoginMicrosoft.innerHTML = "Abrindo Janela...";
            btnLoginMicrosoft.disabled = true;
            
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage(JSON.stringify({
                    action: 'loginMicrosoft'
                }));
            }
            
            // Reativar o botão depois de um tempo caso o popup seja fechado
            setTimeout(() => {
                btnLoginMicrosoft.innerHTML = originalText;
                btnLoginMicrosoft.disabled = false;
            }, 3000);
        });
    }

    // ==========================================
    // SKIN UPLOAD LOGIC
    // ==========================================
    const btnUploadSkin = document.getElementById('btn-upload-skin');
    const skinUploadInput = document.getElementById('skin-upload-input');
    const btnConfirmUpload = document.getElementById('btn-confirm-upload');
    const skinUploadContainer = document.getElementById('skin-upload-container');
    let selectedSkinFile = null;

    if (btnUploadSkin && skinUploadInput && btnConfirmUpload) {
        btnUploadSkin.addEventListener('click', () => skinUploadInput.click());

        skinUploadInput.addEventListener('change', (e) => {
            const file = e.target.files[0];
            if (!file) return;
            selectedSkinFile = file;
            const reader = new FileReader();
            reader.onload = (event) => {
                if (skinViewer) {
                    skinViewer.loadSkin(event.target.result).catch(err => console.error("Erro preview:", err));
                }
                btnUploadSkin.innerText = file.name;
                btnConfirmUpload.style.display = 'block';
            };
            reader.readAsDataURL(file);
        });

        btnConfirmUpload.addEventListener('click', async () => {
            if (!selectedSkinFile) return;
            const nick = skinUploadContainer.dataset.nick;
            if (!nick) return showToast("Nick não encontrado.", "error");

            const originalText = btnConfirmUpload.innerText;
            btnConfirmUpload.innerText = "Enviando...";
            btnConfirmUpload.disabled = true;

            try {
                const formData = new FormData();
                formData.append('skin', selectedSkinFile);
                formData.append('nick', nick);

                const response = await fetch('https://brlaucher-api.vercel.app/api/skin/upload', {
                    method: 'POST',
                    body: formData
                });

                if (response.ok) {
                    showToast("Skin atualizada com sucesso!", "success");
                    btnConfirmUpload.style.display = 'none';
                    btnUploadSkin.innerText = "Selecionar Nova Skin";
                    selectedSkinFile = null;
                } else {
                    const errData = await response.json().catch(() => ({}));
                    showToast(errData.mensagem || "Falha no upload.", "error");
                }
            } catch (err) {
                showToast("Erro de conexão: " + err.message, "error");
            } finally {
                btnConfirmUpload.innerText = originalText;
                btnConfirmUpload.disabled = false;
            }
        });
    }

});
