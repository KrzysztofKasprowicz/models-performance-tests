namespace AzureOpenAiBenchmark;

public enum ConversationRole
{
    User,
    Assistant,
}

public sealed record ConversationMessage(ConversationRole Role, string Text);
