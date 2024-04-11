namespace CodeConverterCLI.Models;

public class ScriptConvert
{
  public required string Model { get; set; }
  public required string TargetScript { get; set; }
  public required string SourceScript { get; set; }
  public required string Content { get; set; }
  public required int MaxTokens { get; set; }
}