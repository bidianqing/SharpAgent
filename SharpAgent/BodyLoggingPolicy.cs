using System.ClientModel.Primitives;
using System.Text;

namespace SharpAgent
{
    public class BodyLoggingPolicy : PipelinePolicy
    {
        public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            LogRequest(message.Request);
            ProcessNext(message, pipeline, currentIndex);
            LogResponse(message.Response);
        }

        public override async ValueTask ProcessAsync(
            PipelineMessage message,
            IReadOnlyList<PipelinePolicy> pipeline,
            int currentIndex)
        {
            LogRequest(message.Request);
            await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
            LogResponse(message.Response);
        }

        private void LogRequest(PipelineRequest request)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[REQUEST] {request.Method} {request.Uri}");

            // 打印请求头
            foreach (var header in request.Headers)
            {
                sb.AppendLine($"  {header.Key}: {header.Value}");
            }

            // 打印请求体
            if (request.Content is not null)
            {
                try
                {
                    using var ms = new MemoryStream();
                    request.Content.WriteTo(ms);
                    ms.Position = 0;

                    using var reader = new StreamReader(ms, Encoding.UTF8);
                    string body = reader.ReadToEnd();
                    if (!string.IsNullOrEmpty(body))
                    {
                        sb.AppendLine($"[REQUEST BODY]");
                        sb.AppendLine(body);
                    }
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"[REQUEST BODY] <failed to read: {ex.Message}>");
                }
            }

            Console.WriteLine(sb.ToString());
        }

        private void LogResponse(PipelineResponse response)
        {
            if (response is null) return;

            var sb = new StringBuilder();
            sb.AppendLine($"[RESPONSE] {response.Status} {response.ReasonPhrase}");

            // 打印响应头
            foreach (var header in response.Headers)
            {
                sb.AppendLine($"  {header.Key}: {header.Value}");
            }

            // 打印响应体
            try
            {
                // 方式1：如果响应已被缓冲，直接用 Content 属性
                if (response.Content is not null)
                {
                    string body = response.Content.ToString();
                    if (!string.IsNullOrEmpty(body))
                    {
                        sb.AppendLine($"[RESPONSE BODY]");
                        sb.AppendLine(body);
                    }
                }
                // 方式2：如果 ContentStream 可读，手动读取
                else if (response.ContentStream is not null && response.ContentStream.CanRead)
                {
                    // 注意：流只能读一次，需要复制或重置
                    using var ms = new MemoryStream();
                    response.ContentStream.CopyTo(ms);
                    ms.Position = 0;

                    // 将流还给后续代码（替换原流）
                    response.ContentStream = ms;

                    using var reader = new StreamReader(ms, Encoding.UTF8);
                    string body = reader.ReadToEnd();
                    if (!string.IsNullOrEmpty(body))
                    {
                        sb.AppendLine($"[RESPONSE BODY]");
                        sb.AppendLine(body);
                    }
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[RESPONSE BODY] <failed to read: {ex.Message}>");
            }

            Console.WriteLine(sb.ToString());
        }
    }
}

