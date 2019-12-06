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
function reCaptchaV2Callback(token) {
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
        document.getElementsByClassName("form-signin")[0].submit();
    } else {
        if (document.getElementsByClassName("form-signin")[0].classList.contains("was-validated") === false)
            document.getElementsByClassName("form-signin")[0].classList.add("was-validated");
        grecaptcha.reset();
    }
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
    window.addEventListener("focus", windowHasFocus, false);
    window.addEventListener("blur", windowLostFocus, false);
    window.addEventListener("click", reset, false);
    window.addEventListener("mousemove", reset, false);
    window.addEventListener("keypress", reset, false);
    window.addEventListener("scroll", reset, false);
    document.addEventListener("touchMove", reset, false);
    document.addEventListener("touchEnd", reset, false);
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
var refresh_rate = 60 * 5
var last_user_action = 0;
var has_focus = false;
var lost_focus_count = 0; 
var focus_margin = 10;

function reset() {
    last_user_action = 0;
    updateVisualTimer('Reset Timer');
}

function updateVisualTimer(value) {
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
    updateVisualTimer();
}, 1000);

function refreshCheck() {
    var focus = window.onfocus;
    if ((last_user_action >= refresh_rate && !has_focus && document.readyState == "complete") || lost_focus_count > focus_margin) {
        window.location.reload();
        reset();
    }

}