using EbedrendeloApp.Common.Security;
using EbedrendeloApp.Common.Results;
using MediatR;

namespace EbedrendeloApp.Features.Billing.GeneratePeriodInvoices;

/// <summary>
/// Kiszámlázza az időszak összes olyan aktív menürendelését, amelyhez még nem tartozik számla.
/// Újrafuttatható: másodszorra csak az azóta keletkezett (B-fázisú) rendelésekről készít kiegészítő
/// számlát, a korábban kiszámlázott napokat nem érinti.
/// </summary>
public sealed record GeneratePeriodInvoicesCommand(int OrderingPeriodId, int GeneratedByUserId)
    : IRequest<Result<BatchInvoiceResult>>, IRequireAdmin;

public sealed record BatchInvoiceResult(IReadOnlyList<GeneratedInvoiceDto> Generated);

/// <param name="SequenceNumber">1 = alapszámla, 2+ = kiegészítő számla.</param>
/// <param name="DayCount">Hány menünap került rá.</param>
public sealed record GeneratedInvoiceDto(
    int InvoiceId,
    int UserId,
    int SequenceNumber,
    int DayCount,
    int GrossHuf,
    int CreditAppliedHuf,
    int PayableHuf);
