namespace AzureOpenAiBenchmark;

public static class Prompts
{
    public const string SystemPrompt =
        "You are a precise, efficient assistant. Answer the user's latest message directly, in " +
        "plain English prose only — no Markdown, no lists, no headings. Stay between 60 and 100 " +
        "words regardless of how complex the question is.";

    public const string SingleTurnUserPrompt =
        "A warehouse network has three sites. Site A starts with 420 units and gains 35 units per " +
        "day. Site B starts with 290 units and gains 52 units per day. Site C starts with 610 units " +
        "but loses 18 units per day to spoilage. After how many full days does Site B first hold " +
        "more units than Site C, and which site holds the most units at that point? Give the day " +
        "count, the site, and a one-sentence justification.";

    /// <summary>Fixed user turns for the multi-turn scenario, sent in order.</summary>
    public static readonly string[] MultiTurnUserMessages =
    [
        "I'm planning a 3-day trip to Kraków in October for two adults. Suggest a rough day-by-day " +
        "plan covering the Old Town, Wieliczka Salt Mine, and one evening with local food.",

        "Our budget is tighter than I expected — about 150 EUR per person for the whole trip " +
        "excluding flights and hotel. Adjust the plan to cut costs without dropping the salt mine.",

        "One of us is vegetarian and avoids gluten. Update the food recommendations across all " +
        "three days to fit that, and give a short final summary of the full itinerary.",
    ];

    /// <summary>
    /// Fixed, canned "assistant" replies injected into history between user turns so every test
    /// cell sees byte-identical conversation context. These are NOT the model's real output —
    /// the real streamed response is what gets measured, this text only shapes what the next
    /// turn sees as history. One fewer entry than <see cref="MultiTurnUserMessages"/>: there is
    /// no placeholder needed after the last turn.
    /// </summary>
    public static readonly string[] MultiTurnFixedAssistantReplies =
    [
        "Day 1: Old Town walking tour covering the Main Square, Cloth Hall, and St. Mary's " +
        "Basilica, followed by dinner at a milk bar. Day 2: Half-day guided tour of the Wieliczka " +
        "Salt Mine, then a free afternoon in Kazimierz. Day 3: Schindler's Factory museum in the " +
        "morning, then a farewell dinner of traditional pierogi and żurek near the Vistula.",

        "Swap the guided Wieliczka tour for the cheaper self-guided entry ticket, replace the " +
        "milk-bar dinner with a grocery-store picnic in Planty Park, and use the Kraków city card " +
        "for free public transport and discounted museum entry on day 3.",
    ];
}
