using Core.Domain;

namespace Core.Abstractions
{
    public interface ICpiRepository
    {
        IReadOnlyList<CpiPoint> GetMonthlyCpi();
    }
}
