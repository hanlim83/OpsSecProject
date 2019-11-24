function SLOCountDown(secs) {
    var countDownInterval = function () {
        if (secs <= 1) {
            clearInterval(interval);
            window.location.replace(document.getElementById("sloLink").href);
        } else {
            secs--;
            document.getElementById('sloLink').innerHTML = "Yes ("+secs+" seconds remaining till auto logout)";
        }
    };
    var interval = setInterval(countDownInterval, 1000);
}
(function () {
    'use strict';
    window.addEventListener('load', function () {
        var forms = document.getElementsByClassName('needs-validation');
        var validation = Array.prototype.filter.call(forms, function (form) {
            form.addEventListener('submit', function (event) {
                if (form.checkValidity() === false) {
                    event.preventDefault();
                    event.stopPropagation();
                }
                form.classList.add('was-validated');
            }, false);
        });
    }, false);
})();