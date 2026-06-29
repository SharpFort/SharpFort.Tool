using System.Reflection;
using Microsoft.Extensions.CommandLineUtils;
using Volo.Abp.DependencyInjection;

namespace SharpFort.Tool
{
    public class CommandInvoker : ISingletonDependency
    {
        private readonly IEnumerable<ICommand> _commands;
        private CommandLineApplication Application { get; }

        public CommandInvoker(IEnumerable<ICommand> commands)
        {
            _commands = commands;
            Application = new CommandLineApplication();
            InitCommand();
        }

        private void InitCommand()
        {    
            Application.HelpOption("-h|--help");
            var asm = Assembly.GetExecutingAssembly();
            var version = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                          ?? asm.GetName().Version?.ToString()
                          ?? "1.0.0";
            if (version.Contains('+')) version = version.Split('+')[0];

            Application.VersionOption("-v|--versions", version);
            foreach (var command in _commands)
            {
                CommandLineApplication childrenCommandLineApplication = new CommandLineApplication(true)
                {
                    Name = command.Command,
                    Parent = Application,
                    Description = command.Description
                };
                Application.Commands.Add(childrenCommandLineApplication);
                command.CommandLineApplication(childrenCommandLineApplication);
            }
        }

        public Task InvokerAsync(string[] args)
        {
            Application.Execute(args);
            return Task.CompletedTask;
        }
    }
}