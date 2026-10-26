(function () {
    'use strict';

    const source = document.getElementById('statistics-data');
    if (!source) return;
    if (typeof Chart === 'undefined') {
        document.getElementById('chart-error').classList.remove('d-none');
        return;
    }

    const data = JSON.parse(source.textContent);
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const sharedOptions = { responsive: true, maintainAspectRatio: false, animation: reduceMotion ? false : { duration: 400 } };

    new Chart(document.getElementById('release-years-chart'), {
        type: 'bar',
        data: {
            labels: data.years.map(item => item.Year),
            datasets: [{
                label: 'Кількість фільмів',
                data: data.years.map(item => item.Count),
                backgroundColor: '#0d6efd',
                borderRadius: 4
            }]
        },
        options: {
            ...sharedOptions,
            plugins: { legend: { display: false } },
            scales: {
                x: { title: { display: true, text: 'Рік випуску' } },
                y: { beginAtZero: true, ticks: { precision: 0 }, title: { display: true, text: 'Кількість фільмів' } }
            }
        }
    });

    const genresCanvas = document.getElementById('genres-chart');
    if (!genresCanvas) return;
    new Chart(genresCanvas, {
        type: 'doughnut',
        data: {
            labels: data.genres.map(item => item.Name),
            datasets: [{
                label: 'Кількість фільмів',
                data: data.genres.map(item => item.Count),
                backgroundColor: data.genres.map((_, index) => `hsl(${(index * 137.508) % 360}, 65%, 47%)`),
                borderWidth: 2,
                hoverOffset: 6
            }]
        },
        options: {
            ...sharedOptions,
            plugins: { legend: { position: 'bottom', labels: { boxWidth: 12, padding: 12 } } }
        }
    });
}());
