using SharpFort.Tool.Application.Contracts.Dtos;

namespace SharpFort.Tool.Application.Contracts
{
    public interface ITemplateGenService
    {
        Task<byte[]> CreateModuleAsync(TemplateGenCreateInputDto moduleCreateInputDto);
        Task<List<string>> GetAllTemplatesAsync();
        Task<Stream> PreviewTemplateAsync(string branch);

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        (int zipCount, int metaCount, long totalBytes) ClearCache();

        /// <summary>
        /// 刷新所有缓存（重新下载）
        /// </summary>
        Task<int> RefreshCacheAsync();

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        (int zipCount, long totalBytes) GetCacheStats();
    }
}
