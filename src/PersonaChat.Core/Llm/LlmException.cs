namespace PersonaChat.Core.Llm;

/// <summary>Its message is safe to show to the user; details belong in the log.</summary>
public sealed class LlmException(string message, Exception? inner = null) : Exception(message, inner);
