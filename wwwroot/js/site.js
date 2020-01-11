function SLOCountDown(secs) {
    var countDownInterval = function () {
        if (window.location.pathname != "/Landing/Logout" && window.location.pathname != "/Account/Logout")
            return;
        else if (secs < 1) {
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
function reCaptchaCallback(token) {
    var tokenInput = document.getElementById("recaptchaResponse");
    if (tokenInput == null && token != null) {
        tokenInput = document.createElement("input");
        tokenInput.setAttribute("id", "recaptchaResponse");
        tokenInput.setAttribute("type", "hidden");
        tokenInput.setAttribute("name", "recaptchaResponse");
        tokenInput.setAttribute("value", token);
        document.getElementsByClassName("form-signin")[0].appendChild(tokenInput);
    } else if (token != null)
        tokenInput.setAttribute("value", token);
    document.getElementsByClassName("form-signin")[0].checkValidity();
    if (document.getElementsByClassName("form-signin")[0].checkValidity() === true) {
        document.getElementById('formBtnSubmit').setAttribute("disabled", "disabled");
        document.getElementsByClassName("form-signin")[0].submit();
    } else {
        if (document.getElementsByClassName("form-signin")[0].classList.contains("was-validated") === false)
            document.getElementsByClassName("form-signin")[0].classList.add("was-validated");
        grecaptcha.reset();
    }
}
(function () {
    'use strict';
    if (Turbolinks.supported) {
        document.addEventListener('turbolinks:load', function () {
            var path = window.location.pathname;
            var checkCollapse = $('a[href="' + path + '"]').hasClass("collapse-item");
            var checkNormal = $('a[href="' + path + '"]').hasClass("nav-link");
            if (checkNormal) {
                $('a[href="' + path + '"]').parent().addClass('active');
            } else if (checkCollapse) {
                $('a[href="' + path + '"]').addClass('active');
                $('a[href="' + path + '"]').parent().parent().parent().addClass('active');
                if (collapse === false) {
                    $('a[href="' + path + '"]').parent().parent().parent().children().eq(0).attr("aria-expanded", "true");
                    $('a[href="' + path + '"]').parent().parent().parent().children().eq(0).removeClass("collapsed");
                    $('a[href="' + path + '"]').parent().parent().parent().children().eq(1).addClass("show");
                }
            }
            var forms = document.getElementsByClassName('needs-validation');
            Array.prototype.filter.call(forms, function (form) {
                form.addEventListener('submit', function (event) {
                    form.classList.add('was-validated');
                    if (form.checkValidity() === false) {
                        event.preventDefault();
                        event.stopPropagation();
                    }
                    else if (form.checkValidity() === true) {
                        document.getElementById('searchBtnSubmit').setAttribute("disabled", "disabled");
                        document.getElementById('formBtnSubmit').setAttribute("disabled", "disabled");
                    }
                }, false);
            });
            var canvas = document.getElementById("particles");
            if (canvas !== null && Particles.options == null) {
                Particles.init({
                    selector: '.background',
                    color: '#75A5B7',
                    maxParticles: 130,
                    connectParticles: true,
                    responsive: [
                        {
                            breakpoint: 768,
                            options: {
                                maxParticles: 80
                            }
                        }, {
                            breakpoint: 375,
                            options: {
                                maxParticles: 50
                            }
                        }
                    ]
                });
            }
            if (window.location.pathname.includes("/Landing/") || window.location.pathname.includes("/Internal/Account/"))
                grecaptcha.render('formBtnSubmit');
        }, false);
    } else {
        $(document).ready(function () {
            var path = window.location.pathname;
            var checkCollapse = $('a[href="' + path + '"]').hasClass("collapse-item");
            var checkNormal = $('a[href="' + path + '"]').hasClass("nav-link");
            if (checkNormal) {
                $('a[href="' + path + '"]').parent().addClass('active');
            } else if (checkCollapse) {
                $('a[href="' + path + '"]').addClass('active');
                $('a[href="' + path + '"]').parent().parent().parent().addClass('active');
                if (collapse === false) {
                    $('a[href="' + path + '"]').parent().parent().parent().children().eq(0).attr("aria-expanded", "true");
                    $('a[href="' + path + '"]').parent().parent().parent().children().eq(0).removeClass("collapsed");
                    $('a[href="' + path + '"]').parent().parent().parent().children().eq(1).addClass("show");
                }
            }
            var forms = document.getElementsByClassName('needs-validation');
            Array.prototype.filter.call(forms, function (form) {
                form.addEventListener('submit', function (event) {
                    form.classList.add('was-validated');
                    if (form.checkValidity() === false) {
                        event.preventDefault();
                        event.stopPropagation();
                    }
                    else if (form.checkValidity() === true) {
                        document.getElementById('searchBtnSubmit').setAttribute("disabled", "disabled");
                        document.getElementById('formBtnSubmit').setAttribute("disabled", "disabled");
                    }
                }, false);
            });
            var canvas = document.getElementById("particles");
            if (canvas !== null && Particles.options == null) {
                Particles.init({
                    selector: '.background',
                    color: '#75A5B7',
                    maxParticles: 130,
                    connectParticles: true,
                    responsive: [
                        {
                            breakpoint: 768,
                            options: {
                                maxParticles: 80
                            }
                        }, {
                            breakpoint: 375,
                            options: {
                                maxParticles: 50
                            }
                        }
                    ]
                });
            }
            if (window.location.pathname.includes("/Landing/") || window.location.pathname.includes("/Internal/Account/"))
                grecaptcha.render('formBtnSubmit');
        });
    }
    window.addEventListener("focus", windowHasFocus, false);
    window.addEventListener("blur", windowLostFocus, false);
    window.addEventListener("click", reset, false);
    window.addEventListener("mousemove", reset, false);
    window.addEventListener("keypress", reset, false);
    window.addEventListener("scroll", reset, false);
    document.addEventListener("touchMove", reset, false);
    document.addEventListener("touchEnd", reset, false);
})();
var refresh_rate = 60 * 5
var last_user_action = 0;
var has_focus = false;
var lost_focus_count = 0;
var focus_margin = 10;

function reset() {
    last_user_action = 0;
    updateTimer('Reset Timer');
}

function updateTimer(value) {
    if (value) {
        console.log(value);
    } else if (has_focus) {
        console.log("User has focus won't refresh");
    } else if (last_user_action >= refresh_rate) {
        console.log("Refreshing");
    } else {
        console.log(refresh_rate - last_user_action);
    }
}

function windowHasFocus() {
    has_focus = true;
}

function windowLostFocus() {
    has_focus = false;
    lost_focus_count++;
    console.log(lost_focus_count + " <~ Lost Focus");
}

setInterval(function () {
    last_user_action++;
    refreshCheck();
    updateTimer();
}, 1000);

function refreshCheck() {
    if ((last_user_action >= refresh_rate && !has_focus && document.readyState == "complete") || lost_focus_count > focus_margin) {
        if (Turbolinks.supported)
            Turbolinks.visit(window.location.pathname, { action: "replace" });
        else
            window.location.reload();
        reset();
    }
}
function number_format(number, decimals, dec_point, thousands_sep) {
    // *     example: number_format(1234.56, 2, ',', ' ');
    // *     return: '1 234,56'
    number = (number + '').replace(',', '').replace(' ', '');
    var n = !isFinite(+number) ? 0 : +number,
        prec = !isFinite(+decimals) ? 0 : Math.abs(decimals),
        sep = (typeof thousands_sep === 'undefined') ? ',' : thousands_sep,
        dec = (typeof dec_point === 'undefined') ? '.' : dec_point,
        s = '',
        toFixedFix = function (n, prec) {
            var k = Math.pow(10, prec);
            return '' + Math.round(n * k) / k;
        };
    // Fix for IE parseFloat(0.55).toFixed(0) = 0;
    s = (prec ? toFixedFix(n, prec) : '' + Math.round(n)).split('.');
    if (s[0].length > 3) {
        s[0] = s[0].replace(/\B(?=(?:\d{3})+(?!\d))/g, sep);
    }
    if ((s[1] || '').length < prec) {
        s[1] = s[1] || '';
        s[1] += new Array(prec - s[1].length + 1).join('0');
    }
    return s.join(dec);
}