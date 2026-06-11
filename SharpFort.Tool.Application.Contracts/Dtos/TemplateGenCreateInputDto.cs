using SharpFort.Tool.Domain.Shared.Enums;

namespace SharpFort.Tool.Application.Contracts.Dtos
{
    public class TemplateGenCreateInputDto
    {
        public string Name { get; set; }
        public string ModuleSoure { get; set; }
        public DbmsEnum Dbms { get; set; }
        public bool NoCache { get; set; }
        public Dictionary<string, string> ReplaceStrData { get; set; } = new();

        public void SetNameReplace()
        {
            ReplaceStrData.Add("SharpFort", Name);
        }
    }
}
