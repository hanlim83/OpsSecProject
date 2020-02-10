using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace OpsSecProject.Helpers
{
    public class GoogleRecaptchaHelper
    {
        public static async Task<bool> IsReCaptchaV2PassedAsync(string recaptchaResponse)
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

        public bool result { get; set; }
        public string score { get; set; }

        public async Task VerifyReCaptchaV3Async(string recaptchaResponse)
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
                    result = false;
                    score = "-1";
                }
                string JSONres = res.Content.ReadAsStringAsync().Result;
                dynamic JSONdata = JObject.Parse(JSONres);
                if (JSONdata.success != "true")
                {
                    result = false;
                    score = "-1";
                } else
                {
                    result = true;
                    score = JSONdata.score;
                }
            }
        }
    }
}
