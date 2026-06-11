using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mapster;
using Volo.Abp.DependencyInjection;
using SharpFort.Tool.Application.Contracts;
using SharpFort.Tool.Application.Contracts.Dtos;
using SharpFort.Tool.Domain;
using SharpFort.Tool.Domain.Shared.Dtos;

namespace SharpFort.Tool.Application
{
    public class TemplateGenService : ITemplateGenService, ITransientDependency
    {
        private readonly TemplateGenManager _templateGenManager;

        public TemplateGenService(TemplateGenManager templateGenManager)
        {
            _templateGenManager = templateGenManager;
        }

        /// <summary>
        /// 下载模块文件
        /// </summary>
        public async Task<byte[]> CreateModuleAsync(TemplateGenCreateInputDto moduleCreateInputDto)
        {
            moduleCreateInputDto.SetNameReplace();

            // 模块类型，就是分支小写
            var input = moduleCreateInputDto.Adapt<TemplateGenCreateDto>();
            input.SetTemplateGiteeRef(moduleCreateInputDto.ModuleSoure);

            var filePath = await _templateGenManager.CreateTemplateAsync(input);

            return await File.ReadAllBytesAsync(filePath);
        }

        /// <summary>
        /// 获取全部模板列表
        /// </summary>
        public async Task<List<string>> GetAllTemplatesAsync()
        {
            return await _templateGenManager.GetAllTemplatesAsync();
        }

        /// <summary>
        /// 预览模板结构
        /// </summary>
        public async Task<Stream> PreviewTemplateAsync(string branch)
        {
            return await _templateGenManager.GetTemplateStreamForPreviewAsync(branch);
        }

        /// <summary>
        /// 清空所有缓存
        /// </summary>
        public (int zipCount, int metaCount, long totalBytes) ClearCache()
        {
            return _templateGenManager.ClearCache();
        }

        /// <summary>
        /// 刷新所有缓存（重新下载）
        /// </summary>
        public async Task<int> RefreshCacheAsync()
        {
            return await _templateGenManager.RefreshCacheAsync();
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public (int zipCount, long totalBytes) GetCacheStats()
        {
            return _templateGenManager.GetCacheStats();
        }
    }
}
