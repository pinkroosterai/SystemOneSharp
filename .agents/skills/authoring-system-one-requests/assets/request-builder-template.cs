using SystemOneSharp;

var request = new SystemOneRequestBuilder()
    .WithState("Please refund my duplicate charge")
    .AddChoice("team", "Which team should handle this?", choice => choice
        .Option("billing", "Payments, charges, invoices, and refunds")
        .Option("support", "Product usage, bugs, and technical issues")
        .Option("other", "None of the listed teams clearly applies"))
    .AddScore("urgency", "How urgent is this?", score => score
        .Level("Routine; no time pressure")
        .Level("Needs attention soon")
        .Level("Time-sensitive or likely to escalate"))
    .AddNoul("refund", "Does the customer explicitly request a refund?")
    .Build();
