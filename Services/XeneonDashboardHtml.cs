namespace SysGlance.Services;

/// <summary>
/// Generates the self-contained HTML dashboard for the Xeneon Edge widget.
/// </summary>
internal static class XeneonDashboardHtml
{
    /// <summary>
    /// Returns a complete HTML page that fetches metrics from the local server and renders gauge cards.
    /// </summary>
    public static string Generate(int port)
    {
        return $$"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
        <meta charset="UTF-8">
        <meta name="viewport" content="width=device-width, initial-scale=1.0">
        <title>SysGlance</title>
        <style>
            :root { --fs: 1; }
            * { margin: 0; padding: 0; box-sizing: border-box; }
            body {
                background: #0A0A0A;
                font-family: 'Segoe UI', system-ui, sans-serif;
                color: #fff;
                width: 100vw;
                height: 100vh;
                overflow: hidden;
                display: flex;
                flex-direction: column;
            }
            .carousel {
                position: relative;
                overflow: hidden;
                width: 100%;
                flex: 1;
                min-height: 0;
            }
            .track {
                display: flex;
                transition: transform 0.4s ease;
                height: 100%;
                width: 100%;
            }
            .page {
                min-width: 100%;
                flex-shrink: 0;
                height: 100%;
                display: grid;
                gap: 10px;
                padding: 10px;
                justify-content: center;
                align-content: center;
            }
            .dots {
                display: flex;
                gap: 8px;
                padding: 6px 0 8px;
                justify-content: center;
            }
            .dots.hidden { display: none; }
            .dot {
                width: 8px;
                height: 8px;
                border-radius: 50%;
                background: #333;
                cursor: pointer;
                transition: background 0.3s;
            }
            .dot.active { background: #888; }
            .card, .counter-card {
                background: #141414;
                border-radius: 16px;
                display: flex;
                flex-direction: column;
                align-items: center;
                justify-content: center;
                position: relative;
                min-height: 0;
                overflow: hidden;
                padding: 8px;
            }
            .gauge-wrapper {
                position: relative;
                width: 68%;
                aspect-ratio: 1;
            }
            .gauge-wrapper svg {
                width: 100%;
                height: 100%;
            }
            .gauge-label {
                font-size: calc(clamp(10px, 2.2vmin, 16px) * var(--fs));
                font-weight: 600;
                letter-spacing: 1.5px;
                text-transform: uppercase;
                color: #888;
                margin-bottom: 4px;
            }
            .gauge-value {
                position: absolute;
                top: 50%;
                left: 50%;
                transform: translate(-50%, -50%);
                font-weight: 700;
                font-family: 'Consolas', 'Courier New', monospace;
                white-space: nowrap;
                text-align: center;
            }
            .counter-value {
                font-weight: 700;
                font-family: 'Consolas', 'Courier New', monospace;
                margin-top: 4px;
                white-space: nowrap;
            }
            .error-msg {
                position: fixed;
                bottom: 8px;
                left: 50%;
                transform: translateX(-50%);
                font-size: 10px;
                color: #555;
                display: none;
            }
        </style>
        </head>
        <body>
        <div class="carousel" id="carousel">
            <div class="track" id="track"></div>
        </div>
        <div class="dots hidden" id="dots"></div>
        <div class="error-msg" id="errorMsg">Connection lost</div>
        <script>
        const PORT = {{port}};
        const ENDPOINT = `http://localhost:${PORT}/metrics`;

        const PERCENT_KEYS = new Set([
            'CpuUsage','GpuUsage','GpuPowerPercent','GpuFanPercent',
            'GpuFan2Percent','GpuFbUsage','GpuVidUsage','GpuBusUsage','Gpu1Usage',
            'Cpu1Usage','Cpu2Usage','Cpu3Usage','Cpu4Usage',
            'Cpu5Usage','Cpu6Usage','Cpu7Usage','Cpu8Usage',
            'Cpu9Usage','Cpu10Usage','Cpu11Usage','Cpu12Usage',
            'Cpu13Usage','Cpu14Usage','Cpu15Usage','Cpu16Usage'
        ]);

        const TEMP_KEYS = new Set([
            'CpuTemp','GpuTemp','GpuHotSpot','Gpu1Temp',
            'Cpu1Temp','Cpu2Temp','Cpu3Temp','Cpu4Temp',
            'Cpu5Temp','Cpu6Temp','Cpu7Temp','Cpu8Temp',
            'Cpu9Temp','Cpu10Temp','Cpu11Temp','Cpu12Temp',
            'Cpu13Temp','Cpu14Temp','Cpu15Temp','Cpu16Temp'
        ]);

        const FPS_KEYS = new Set([
            'Fps','FpsMin','FpsAvg','FpsMax','Fps1Low','Fps01Low'
        ]);

        function getGaugeType(key) {
            if (PERCENT_KEYS.has(key)) return 'percent';
            if (TEMP_KEYS.has(key)) return 'temp';
            if (FPS_KEYS.has(key)) return 'counter';
            return 'gauge';
        }

        function getGaugeFraction(key, rawValue) {
            if (PERCENT_KEYS.has(key)) return Math.min(rawValue / 100, 1);
            if (TEMP_KEYS.has(key)) return Math.min(rawValue / 110, 1);
            return Math.min(rawValue / 100, 1);
        }

        // SVG arc gauge parameters.
        const RADIUS = 44;
        const STROKE = 6;
        const CX = 50;
        const CY = 50;
        const START_ANGLE = 135;
        const SWEEP = 270;

        function polarToCartesian(cx, cy, r, angleDeg) {
            const rad = (angleDeg * Math.PI) / 180;
            return { x: cx + r * Math.cos(rad), y: cy + r * Math.sin(rad) };
        }

        function describeArc(cx, cy, r, startAngle, sweepAngle) {
            const endAngle = startAngle + sweepAngle;
            const start = polarToCartesian(cx, cy, r, startAngle);
            const end = polarToCartesian(cx, cy, r, endAngle);
            const largeArc = Math.abs(sweepAngle) > 180 ? 1 : 0;
            const sweepFlag = sweepAngle > 0 ? 1 : 0;
            return `M ${start.x} ${start.y} A ${r} ${r} 0 ${largeArc} ${sweepFlag} ${end.x} ${end.y}`;
        }

        function valueFontSize(text) {
            const len = text.length;
            if (len <= 3) return 'calc(clamp(16px, 4.5vmin, 32px) * var(--fs))';
            if (len <= 5) return 'calc(clamp(13px, 3.5vmin, 26px) * var(--fs))';
            if (len <= 7) return 'calc(clamp(11px, 2.8vmin, 20px) * var(--fs))';
            return 'calc(clamp(9px, 2.2vmin, 16px) * var(--fs))';
        }

        function counterFontSize(text) {
            const len = text.length;
            if (len <= 3) return 'calc(clamp(24px, 7vmin, 48px) * var(--fs))';
            if (len <= 5) return 'calc(clamp(18px, 5.5vmin, 38px) * var(--fs))';
            if (len <= 7) return 'calc(clamp(14px, 4vmin, 28px) * var(--fs))';
            return 'calc(clamp(11px, 3vmin, 22px) * var(--fs))';
        }

        function createGaugeCard(metric) {
            const type = getGaugeType(metric.key);

            if (type === 'counter') {
                return `<div class="counter-card" data-key="${metric.key}">
                    <div class="gauge-label">${metric.label}</div>
                    <div class="counter-value" data-value style="color:${metric.color};font-size:${counterFontSize(metric.value)}">${metric.value}</div>
                </div>`;
            }

            const fraction = getGaugeFraction(metric.key, metric.rawValue);
            const fillSweep = SWEEP * fraction;
            const bgArc = describeArc(CX, CY, RADIUS, START_ANGLE, SWEEP);
            const fillArc = describeArc(CX, CY, RADIUS, START_ANGLE, Math.max(fillSweep, 0.1));

            return `<div class="card" data-key="${metric.key}">
                <div class="gauge-label">${metric.label}</div>
                <div class="gauge-wrapper">
                    <svg viewBox="0 0 100 100">
                        <path d="${bgArc}" fill="none" stroke="#2A2A2A" stroke-width="${STROKE}" stroke-linecap="round"/>
                        <path data-fill d="${fillArc}" fill="none" stroke="${metric.color}" stroke-width="${STROKE}" stroke-linecap="round"/>
                    </svg>
                    <div class="gauge-value" data-value style="color:${metric.color};font-size:${valueFontSize(metric.value)}">${metric.value}</div>
                </div>
            </div>`;
        }

        function updateCard(el, metric) {
            const type = getGaugeType(metric.key);
            const valueEl = el.querySelector('[data-value]');

            if (valueEl) {
                valueEl.textContent = metric.value;
                valueEl.style.color = metric.color;
                valueEl.style.fontSize = type === 'counter'
                    ? counterFontSize(metric.value)
                    : valueFontSize(metric.value);
            }

            if (type !== 'counter') {
                const fillPath = el.querySelector('[data-fill]');
                if (fillPath) {
                    const fraction = getGaugeFraction(metric.key, metric.rawValue);
                    const fillSweep = SWEEP * fraction;
                    fillPath.setAttribute('d', describeArc(CX, CY, RADIUS, START_ANGLE, Math.max(fillSweep, 0.1)));
                    fillPath.setAttribute('stroke', metric.color);
                }
            }
        }

        // Find the best grid layout (cols x rows) for N cards that maximises card size.
        function bestLayout(count) {
            const c = document.getElementById('carousel');
            const w = c.offsetWidth - 20;
            const h = c.offsetHeight - 20;
            const gap = 10;
            let bestCols = 1;
            let bestSize = 0;

            for (let cols = 1; cols <= count; cols++) {
                const rows = Math.ceil(count / cols);
                const cardW = (w - gap * (cols - 1)) / cols;
                const cardH = (h - gap * (rows - 1)) / rows;
                const size = Math.min(cardW, cardH);
                if (size > bestSize) {
                    bestSize = size;
                    bestCols = cols;
                }
            }

            const bestRows = Math.ceil(count / bestCols);
            return { cols: bestCols, rows: bestRows, size: Math.floor(bestSize) };
        }

        // Calculate how many cards fit in a single page based on the actual container size.
        function getCardsPerPage() {
            const c = document.getElementById('carousel');
            const w = c.offsetWidth - 20;
            const h = c.offsetHeight - 20;
            const gap = 10;
            const minCard = 120;
            const cols = Math.max(1, Math.floor((w + gap) / (minCard + gap)));
            const rows = Math.max(1, Math.floor((h + gap) / (minCard + gap)));
            return cols * rows;
        }

        // Carousel state.
        let currentPage = 0;
        let totalPages = 1;
        let perPage = 6;

        function goToPage(index) {
            currentPage = Math.max(0, Math.min(index, totalPages - 1));
            document.getElementById('track').style.transform = `translateX(-${currentPage * 100}%)`;
            document.querySelectorAll('.dot').forEach((dot, i) => {
                dot.classList.toggle('active', i === currentPage);
            });
        }

        // Touch swipe support.
        let touchStartX = 0;
        let touchDelta = 0;
        const carousel = document.getElementById('carousel');

        carousel.addEventListener('touchstart', (e) => {
            touchStartX = e.touches[0].clientX;
            touchDelta = 0;
            document.getElementById('track').style.transition = 'none';
        }, { passive: true });

        carousel.addEventListener('touchmove', (e) => {
            touchDelta = e.touches[0].clientX - touchStartX;
            const cw = carousel.offsetWidth;
            const offset = -(currentPage * cw) + touchDelta;
            document.getElementById('track').style.transform = `translateX(${offset}px)`;
        }, { passive: true });

        carousel.addEventListener('touchend', () => {
            document.getElementById('track').style.transition = 'transform 0.4s ease';
            if (Math.abs(touchDelta) > 50) {
                if (touchDelta < 0 && currentPage < totalPages - 1) {
                    goToPage(currentPage + 1);
                } else if (touchDelta > 0 && currentPage > 0) {
                    goToPage(currentPage - 1);
                } else {
                    goToPage(currentPage);
                }
            } else {
                goToPage(currentPage);
            }
        });

        function applyColors(data) {
            if (data.bgColor) document.body.style.background = data.bgColor;
            if (data.cardColor) {
                document.querySelectorAll('.card, .counter-card').forEach(c => c.style.background = data.cardColor);
            }
            if (data.fontScale != null) {
                document.documentElement.style.setProperty('--fs', data.fontScale);
            }
        }

        // Force rebuild when keys change or container resizes.
        let lastBuiltKey = '';

        function buildPages(metrics) {
            perPage = getCardsPerPage();
            totalPages = Math.max(1, Math.ceil(metrics.length / perPage));

            const track = document.getElementById('track');
            let html = '';
            for (let p = 0; p < totalPages; p++) {
                const pageMetrics = metrics.slice(p * perPage, (p + 1) * perPage);
                const layout = bestLayout(pageMetrics.length);
                html += `<div class="page" style="grid-template-columns:repeat(${layout.cols},${layout.size}px);grid-template-rows:repeat(${layout.rows},${layout.size}px)">`;
                html += pageMetrics.map(createGaugeCard).join('');
                html += '</div>';
            }
            track.innerHTML = html;

            // Build dots.
            const dotsEl = document.getElementById('dots');
            if (totalPages > 1) {
                dotsEl.classList.remove('hidden');
                dotsEl.innerHTML = '';
                for (let i = 0; i < totalPages; i++) {
                    const dot = document.createElement('div');
                    dot.className = 'dot' + (i === currentPage ? ' active' : '');
                    dot.addEventListener('click', () => goToPage(i));
                    dotsEl.appendChild(dot);
                }
            } else {
                dotsEl.classList.add('hidden');
            }

            if (currentPage >= totalPages) {
                currentPage = totalPages - 1;
            }
            goToPage(currentPage);
        }

        // Rebuild on resize so cards adapt to the new container size.
        let resizeTimer;
        window.addEventListener('resize', () => {
            clearTimeout(resizeTimer);
            resizeTimer = setTimeout(() => { lastBuiltKey = ''; }, 200);
        });

        let failCount = 0;

        async function fetchAndUpdate() {
            try {
                const res = await fetch(ENDPOINT);
                const data = await res.json();
                const metrics = data.metrics;
                const errorMsg = document.getElementById('errorMsg');
                errorMsg.style.display = 'none';

                // Server just came back after a meaningful outage — reload so the
                // page initialises cleanly against the new server instance.
                if (failCount >= 8) {
                    location.reload();
                    return;
                }

                failCount = 0;

                const newKey = metrics.map(m => m.key).join(',') + '|' + getCardsPerPage();
                if (newKey !== lastBuiltKey) {
                    lastBuiltKey = newKey;
                    buildPages(metrics);
                } else {
                    const cards = document.querySelectorAll('[data-key]');
                    cards.forEach((card, i) => {
                        if (metrics[i]) updateCard(card, metrics[i]);
                    });
                }

                applyColors(data);
            } catch {
                failCount++;
                document.getElementById('errorMsg').style.display = 'block';
            }
        }

        fetchAndUpdate();
        setInterval(fetchAndUpdate, 500);
        </script>
        </body>
        </html>
        """;
    }
}
