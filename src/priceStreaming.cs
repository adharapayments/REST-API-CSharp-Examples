using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Web.Script.Serialization;

// Note: To enable JSON (JavaScriptSerializer) add following reference: System.Web.Extensions

namespace PriceStreaming
{
    public class hftRequest
    {
        public getAuthorizationChallengeRequest getAuthorizationChallenge { get; set; }
        public getAuthorizationTokenRequest getAuthorizationToken { get; set; }
        public getPriceRequest getPrice { get; set; }
    }

    public class hftResponse
    {
        public getAuthorizationChallengeResponse getAuthorizationChallengeResponse { get; set; }
        public getAuthorizationTokenResponse getAuthorizationTokenResponse { get; set; }
        public getPriceResponse getPriceResponse { get; set; }
    }

    public class getAuthorizationChallengeRequest
    {
        public string user { get; set; }

        public getAuthorizationChallengeRequest(String user)
        {
            this.user = user;
        }
    }

    public class getAuthorizationChallengeResponse
    {
        public string challenge { get; set; }
        public string timestamp { get; set; }
    }

    public class getAuthorizationTokenRequest
    {
        public string user { get; set; }
        public string challengeresp { get; set; }

        public getAuthorizationTokenRequest(String user, String challengeresp)
        {
            this.user = user;
            this.challengeresp = challengeresp;
        }
    }

    public class getAuthorizationTokenResponse
    {
        public string token { get; set; }
        public string timestamp { get; set; }
    }

    public class getPriceRequest
    {
        public string user { get; set; }
        public string token { get; set; }
        public List<string> security { get; set; }
        public List<string> tinterface { get; set; }
        public String granularity { get; set; }
        public int levels { get; set; }
        public int interval { get; set; }

        public getPriceRequest(String user, String token, List<String> security, List<String> tinterface, String granularity, int levels, int interval)
        {
            this.user = user;
            this.token = token;
            this.security = security;
            this.tinterface = tinterface;
            this.granularity = granularity;
            this.levels = levels;
            this.interval = interval;
        }
    }

    public class getPriceResponse
    {
        public int result { get; set; }
        public string message { get; set; }
        public List<priceTick> tick { get; set; }
        public priceHeartbeat heartbeat { get; set; }
        public string timestamp { get; set; }
    }

    public class priceTick
    {
        public string security { get; set; }
        public string tinterface { get; set; }
        public double price { get; set; }
        public int pips { get; set; }
        public int liquidity { get; set; }
        public string side { get; set; }
    }

    public class priceHeartbeat
    {
        public List<string> security { get; set; }
        public List<string> tinterface { get; set; }
    }

    class PriceStreaming
    {

        private static bool ssl = false;
        private static String URL = "/getPrice";
        private static String domain;
        private static String url_stream;
        //private static String url_polling;
        private static String url_challenge;
        private static String url_token;
        private static String user;
        private static String password;
        private static String authentication_port;
        private static String request_port;
        private static String ssl_cert;
        private static String challenge;
        private static String token;
        private static int interval;

        public static void Exec()
        {

            // get properties from file
            getProperties();

            HttpWebRequest httpWebRequest;
            JavaScriptSerializer serializer;
            HttpWebResponse httpResponse;

            X509Certificate certificate1 = null;
            if (ssl)
            {
                using (WebClient client = new WebClient())
                {
                    client.DownloadFile(ssl_cert, "ssl.cert");
                }
                certificate1 = new X509Certificate("ssl.cert");
            }

            // get challenge
            httpWebRequest = (HttpWebRequest)WebRequest.Create(domain + ":" + authentication_port + url_challenge);
            serializer = new JavaScriptSerializer();
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";
            if (ssl)
            {
                httpWebRequest.ClientCertificates.Add(certificate1);
            }
            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                hftRequest request = new hftRequest();
                request.getAuthorizationChallenge = new getAuthorizationChallengeRequest(user);
                streamWriter.WriteLine(serializer.Serialize(request));
            }
            httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                hftResponse response;
                try
                {
                    response = serializer.Deserialize<hftResponse>(streamReader.ReadLine());
                    challenge = response.getAuthorizationChallengeResponse.challenge;
                }
                catch (SocketException ex) { Console.WriteLine(ex.Message); }
                catch (IOException ioex) { Console.WriteLine(ioex.Message); }
            }

            // create challenge response
            String res = challenge;
            char[] passwordArray = password.ToCharArray();
            foreach (byte passwordLetter in passwordArray)
            {
                int value = Convert.ToInt32(passwordLetter);
                string hexOutput = String.Format("{0:X}", value);
                res = res + hexOutput;
            }
            int NumberChars = res.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
            {
                bytes[i / 2] = Convert.ToByte(res.Substring(i, 2), 16);
            }
            SHA1 sha = new SHA1CryptoServiceProvider();
            byte[] tokenArray = sha.ComputeHash(bytes);
            string challengeresp = BitConverter.ToString(tokenArray);
            challengeresp = challengeresp.Replace("-", "");

            // get token with challenge response
            httpWebRequest = (HttpWebRequest)WebRequest.Create(domain + ":" + authentication_port + url_token);
            serializer = new JavaScriptSerializer();
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";
            if (ssl)
            {
                httpWebRequest.ClientCertificates.Add(certificate1);
            }
            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                hftRequest request = new hftRequest();
                request.getAuthorizationToken = new getAuthorizationTokenRequest(user, challengeresp);
                streamWriter.WriteLine(serializer.Serialize(request));
            }
            httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                hftResponse response;
                try
                {
                    response = serializer.Deserialize<hftResponse>(streamReader.ReadLine());
                    token = response.getAuthorizationTokenResponse.token;
                }
                catch (SocketException ex) { Console.WriteLine(ex.Message); }
                catch (IOException ioex) { Console.WriteLine(ioex.Message); }
            }

            // -----------------------------------------
            // Prepare and send a price request
            // -----------------------------------------
            httpWebRequest = (HttpWebRequest)WebRequest.Create(domain + ":" + request_port + url_stream + URL);
            serializer = new JavaScriptSerializer();
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";
            if (ssl)
            {
                httpWebRequest.ClientCertificates.Add(certificate1);
            }
            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                hftRequest request = new hftRequest();

                request.getPrice = new getPriceRequest(user, token, new List<string> { "EUR/USD", "GBP/USD" }, null, "tob", 1, interval);
                streamWriter.WriteLine(serializer.Serialize(request));
            }

            // --------------------------------------------------------------
            // Wait for continuous responses from server (streaming)
            // --------------------------------------------------------------
            httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                hftResponse response;
                String line;
                try
                {
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        response = serializer.Deserialize<hftResponse>(line);
                        if (response.getPriceResponse != null)
                        {
                            if (response.getPriceResponse.timestamp != null)
                            {
                                Console.WriteLine("Response timestamp: " + response.getPriceResponse.timestamp + " Contents:");
                            }
                            if (response.getPriceResponse.tick != null)
                            {
                                foreach (priceTick tick in response.getPriceResponse.tick)
                                {
                                    Console.WriteLine("Security: " + tick.security + " Price: " + tick.price + " tinterface: " + tick.tinterface + " Side: " + tick.side + " Liquidity: " + tick.liquidity);
                                }
                            }
                            if (response.getPriceResponse.heartbeat != null)
                            {
                                Console.WriteLine("Heartbeat!");
                            }
                            if (response.getPriceResponse.message != null)
                            {
                                Console.WriteLine("Message from server: " + response.getPriceResponse.message);
                            }
                        }
                    }
                }
                catch (SocketException ex) { Console.WriteLine(ex.Message); }
                catch (IOException ioex) { Console.WriteLine(ioex.Message); }
            }
        }

        private static void getProperties()
        {
            try
            {
                foreach (var row in File.ReadAllLines("config.properties"))
                {
                    //Console.WriteLine(row);
                    if ("url-stream".Equals(row.Split('=')[0]))
                    {
                        url_stream = row.Split('=')[1];
                    }
                    /*
                    if ("url-polling".Equals(row.Split('=')[0]))
                    {
                        url_polling = row.Split('=')[1];
                    }
                    */
                    if ("url-challenge".Equals(row.Split('=')[0]))
                    {
                        url_challenge = row.Split('=')[1];
                    }
                    if ("url-token".Equals(row.Split('=')[0]))
                    {
                        url_token = row.Split('=')[1];
                    }
                    if ("user".Equals(row.Split('=')[0]))
                    {
                        user = row.Split('=')[1];
                    }
                    if ("password".Equals(row.Split('=')[0]))
                    {
                        password = row.Split('=')[1];
                    }
                    if ("interval".Equals(row.Split('=')[0]))
                    {
                        interval = Int32.Parse(row.Split('=')[1]);
                    }
                    if (ssl)
                    {
                        if ("ssl-domain".Equals(row.Split('=')[0]))
                        {
                            domain = row.Split('=')[1];
                        }
                        if ("ssl-authentication-port".Equals(row.Split('=')[0]))
                        {
                            authentication_port = row.Split('=')[1];
                        }
                        if ("ssl-request-port".Equals(row.Split('=')[0]))
                        {
                            request_port = row.Split('=')[1];
                        }
                        if ("ssl-cert".Equals(row.Split('=')[0]))
                        {
                            ssl_cert = row.Split('=')[1];
                        }
                    }
                    else
                    {
                        if ("domain".Equals(row.Split('=')[0]))
                        {
                            domain = row.Split('=')[1];
                        }
                        if ("authentication-port".Equals(row.Split('=')[0]))
                        {
                            authentication_port = row.Split('=')[1];
                        }
                        if ("request-port".Equals(row.Split('=')[0]))
                        {
                            request_port = row.Split('=')[1];
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}