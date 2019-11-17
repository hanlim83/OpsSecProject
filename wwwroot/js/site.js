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