namespace Core.Services
{
    public static class InflationAdjuster
    {
        // Convert nominal price to "real" price in base-month dollars:
        // real = nominal * (baseCpi / monthCpi)
        public static decimal ToReal(decimal nominal, decimal monthCpi, decimal baseCpi)
            => monthCpi <= 0 ? nominal : nominal * (baseCpi / monthCpi);

        // Convert real price back to nominal:
        // nominal = real * (monthCpi / baseCpi)
        public static decimal ToNominal(decimal real, decimal monthCpi, decimal baseCpi)
            => baseCpi <= 0 ? real : real * (monthCpi / baseCpi);
    }
}
