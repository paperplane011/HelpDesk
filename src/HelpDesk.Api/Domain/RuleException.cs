namespace HelpDesk.Api.Domain;

public sealed class RuleException(string message) : Exception(message);
