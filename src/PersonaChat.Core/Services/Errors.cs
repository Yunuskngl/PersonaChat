namespace PersonaChat.Core.Services;

/// <summary>Its message is safe to show to the user.</summary>
public sealed class InvalidInputException(string message) : Exception(message);

public sealed class PersonaNotFoundException(string id) : Exception($"Unknown persona: {id}");

/// <summary>Its message is safe to show to the user.</summary>
public sealed class ResourceNotFoundException(string message) : Exception(message);
