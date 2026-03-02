using System;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace ARCompletions.Services
{
    public class LineBotService
    {
        private readonly IHttpClientFactory _http;
        private readonly string _token;

        public LineBotService(IHttpClientFactory http, IConfiguration config)
        {
            _http = http;
            _token = config["LINE_CHANNEL_ACCESS_TOKEN"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_token))
                throw new InvalidOperationException("Missing LINE_CHANNEL_ACCESS_TOKEN");
        }

        /// <summary>
        /// Reply a plain text message to a user using the provided reply token.
        /// </summary>
        /// <param name="replyToken">The LINE reply token received in the webhook event. Must be used promptly and only once.</param>
        /// <param name="text">Plain text to send back to the user.</param>
        /// <exception cref="Exception">Throws when the LINE API returns a non-success status.</exception>
        public async Task ReplyText(string replyToken, string text)
        {
            var payload = new
            {
                replyToken,
                messages = new object[] { new { type = "text", text } }
            };
            await Post("https://api.line.me/v2/bot/message/reply", payload);
        }

        /// <summary>
        /// Reply a Flex message to the user.
        /// </summary>
        /// <param name="replyToken">The LINE reply token for this event.</param>
        /// <param name="flexContents">An object representing the Flex message contents (serialized to JSON).</param>
        /// <exception cref="Exception">Throws when the LINE API returns a non-success status.</exception>
        public async Task ReplyFlex(string replyToken, object flexContents)
        {
            var payload = new
            {
                replyToken,
                messages = new object[]
                {
                    new { type="flex", altText="功能選單", contents = flexContents }
                }
            };
            await Post("https://api.line.me/v2/bot/message/reply", payload);
        }

        /// <summary>
        /// Push a plain text message to a user (requires channel access token with push permission).
        /// </summary>
        /// <param name="toUserId">Target user's LINE ID.</param>
        /// <param name="text">Plain text message to deliver.</param>
        /// <exception cref="Exception">Throws when the LINE API returns a non-success status.</exception>
        public async Task PushText(string toUserId, string text)
        {
            var payload = new
            {
                to = toUserId,
                messages = new object[] { new { type = "text", text } }
            };
            await Post("https://api.line.me/v2/bot/message/push", payload);
        }

        private async Task Post(string url, object payload)
        {
            var client = _http.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            var res = await client.PostAsJsonAsync(url, payload);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                throw new Exception($"LINE API failed: {(int)res.StatusCode} {res.ReasonPhrase} {body}");
        }
    }
}
