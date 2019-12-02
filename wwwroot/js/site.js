function SLOCountDown(secs) {
    var countDownInterval = function () {
        if (secs < 1) {
            document.getElementById('sloLink').innerHTML = "Auto Logout in progress";
            var element = document.getElementById("sloReject");
            element.parentNode.removeChild(element);
            clearInterval(interval);
            window.location.replace(document.getElementById("sloLink").href);
        } else if (secs == 1) {
            document.getElementById('sloLink').innerHTML = "Yes (" + secs + " second remaining till auto logout)";
            secs--;
        } else {
            document.getElementById('sloLink').innerHTML = "Yes (" + secs + " seconds remaining till auto logout)";
            secs--;
        }
    };
    var interval = setInterval(countDownInterval, 1000);
}
function reCaptchaV3Callback() {
    grecaptcha.execute("6LfccsUUAAAAAAhL7iOWfm0Fkv9yXcQB3I1UHOOc", { action: 'login' }).then(function (token) {
        var tokenInput = document.createElement("input");
        tokenInput.setAttribute("type", "hidden");
        tokenInput.setAttribute("name", "recaptchaResponse");
        tokenInput.setAttribute("value", token);
        document.getElementsByClassName("form-signin")[0].appendChild(tokenInput);
    });
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
$(document).ready(function () {
    var path = window.location.pathname;
    var checkCollapse = $('a[href="' + path + '"]').hasClass("collapse-item");
    var checkNormal = $('a[href="' + path + '"]').hasClass("nav-link");
    if (checkNormal) {
        $('a[href="' + path + '"]').parent().addClass('active');
    } else if (checkCollapse) {
        $('a[href="' + path + '"]').addClass('active');
        $('a[href="' + path + '"]').parent().parent().parent().addClass('active');
        $('a[href="' + path + '"]').parent().parent().parent().children().eq(0).attr("aria-expanded", "true");
        $('a[href="' + path + '"]').parent().parent().parent().children().eq(0).removeClass("collapsed");
        $('a[href="' + path + '"]').parent().parent().parent().children().eq(1).addClass("show");
    }
});