using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Billing.AddManualCredit;

/// <summary>US-5.2 — admin-issued compensation credit with a mandatory justification (AC 5.2.1).
/// <see cref="AmountHuf"/> must be positive; a manual debit is out of scope (see
/// <see cref="Common.Services.ICreditService.IssueManualCredit"/>).</summary>
public sealed record AddManualCreditCommand(
    int TargetUserId,
    int AmountHuf,
    string Note,
    int PerformedByUserId) : IRequest<Result<int>>;
