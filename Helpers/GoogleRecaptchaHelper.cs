using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace OpsSecProject.Helpers
{
    public static class GoogleRecaptchaHelper
    {
        public static async Task<bool> IsReCaptchaPassedAsync(string recaptchaResponse)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                FormUrlEncodedContent content = new FormUrlEncodedContent(new[]
                    {
                    new KeyValuePair<string, string>("secret", Environment.GetEnvironmentVariable("RECAPTCHAV2_SECRET_KEY")),
                    new KeyValuePair<string, string>("response", recaptchaResponse)
                });

                var res = await httpClient.PostAsync($"https://www.google.com/recaptcha/api/siteverify", content);
                if (res.StatusCode != HttpStatusCode.OK)
                {
                    return false;
                }

                string JSONres = res.Content.ReadAsStringAsync().Result;
                dynamic JSONdata = JObject.Parse(JSONres);
                if (JSONdata.success != "true")
                {
                    return false;
                }

                return true;
            }
        }
        public static async Task<string> ReCaptchaScoreAsync(string recaptchaResponse)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                FormUrlEncodedContent content = new FormUrlEncodedContent(new[]
                    {
                    new KeyValuePair<string, string>("secret", Environment.GetEnvironmentVariable("RECAPTCHAV3_SECRET_KEY")),
                    new KeyValuePair<string, string>("response", recaptchaResponse)
                });

                var res = await httpClient.PostAsync($"https://www.google.com/recaptcha/api/siteverify", content);
                if (res.StatusCode != HttpStatusCode.OK)
                {
                    return "Error";
                }

                string JSONres = res.Content.ReadAsStringAsync().Result;
                dynamic JSONdata = JObject.Parse(JSONres);
                if (JSONdata.success != "true")
                {
                    return "Error";
                }

                return JSONdata.score;
            }
        }
    }
}
