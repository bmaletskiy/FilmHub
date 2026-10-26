$(function () {
    $('.js-genre-select').each(function () {
        var select = $(this);
        select.select2({
            width: '100%',
            placeholder: 'Оберіть жанри',
            minimumInputLength: 3,
            ajax: {
                url: select.data('search-url'),
                dataType: 'json',
                delay: 300,
                data: function (params) { return { term: params.term.trim() }; }
            },
            language: {
                inputTooShort: function () { return 'Введіть щонайменше 3 символи'; },
                noResults: function () { return 'Жанрів не знайдено'; },
                searching: function () { return 'Пошук…'; },
                errorLoading: function () { return 'Не вдалося завантажити жанри. Спробуйте ще раз.'; }
            }
        });
    });
});