namespace petergraves.Integrations.SuperControl;

public sealed record SuperControlApiResponse(
    bool IsSuccess,
    int StatusCode,
    string Body
);
