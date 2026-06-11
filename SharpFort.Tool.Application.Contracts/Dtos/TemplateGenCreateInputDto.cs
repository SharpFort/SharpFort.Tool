using SharpFort.Tool.Domain.Shared.Enums;

namespace SharpFort.Tool.Application.Contracts.Dtos
{
    public class TemplateGenCreateInputDto
    {
        /// <summary>
        /// 模块名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 模板分支来源 (Gitee 分支名)
        /// </summary>
        public string ModuleSoure { get; set; }

        /// <summary>
        /// 数据库提供者
        /// </summary>
        public DbmsEnum Dbms { get; set; }
        public bool NoCache { get; set; }

        /// <summary>
        /// 需要替换的字符串内容
        /// </summary>
        public Dictionary<string, string> ReplaceStrData { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 设置模板占位符替换规则
        /// 将模板中的命名空间占位符替换为实际模块名
        /// 
        /// TODO: Fork 模板仓库后，将占位符从 Yi.Abp/YiAbp 改为 SharpFort
        /// </summary>
        public void SetNameReplace()
        {
            // 模板占位符 → 用户指定的模块名
            // 示例: Name="MyCompany.Crm" → "Yi.Abp"→"MyCompany.Crm", "YiAbp"→"MyCompanyCrm"
            ReplaceStrData.Add("Yi.Abp", Name);
            ReplaceStrData.Add("YiAbp", Name.Replace(".", ""));
        }
    }
}
