using NezamMonitor.Core.Models;

namespace NezamMonitor.Core.Api;

/// <summary>
/// Maps API raw JSON dictionaries to existing Case/Engineer/Fee models.
/// </summary>
public static class ApiExtractor
{
    private static string S(Dictionary<string, object?> d, string k) =>
        d.TryGetValue(k, out var v) ? v?.ToString() ?? "" : "";

    public static Case MapCase(Dictionary<string, object?> api)
    {
        var year = S(api, "das_year");
        var num = S(api, "das_number");
        return new Case
        {
            CaseNumber = $"{year}/{num}",
            Serial = S(api, "das_serial"),
            Owner = $"{S(api, "own_name")} {S(api, "own_famil")}".Trim(),
            OwnerMobile = S(api, "own_mob"),
            Responsibility = S(api, "mas_title"),
            CapacityDate = S(api, "kasr_date"),
            Office = S(api, "daf_name"),
            ReportDate1 = "",
            ReportDate2 = "",
            ReportDate3 = "",
            OwnerFather = S(api, "own_father"),
            OwnerNationalCode = S(api, "own_code_melli"),
            OwnerAddress = S(api, "own_address"),
            OwnerZip = S(api, "own_zip"),
            OwnerTel = S(api, "own_tel"),
            OwnerBirthLoc = S(api, "own_birth_loc"),
            Specification = MapSpecification(api),
        };
    }

    public static CaseSpecification MapSpecification(Dictionary<string, object?> api)
    {
        return new CaseSpecification
        {
            BuildingGroup = S(api, "bg_name"),
            RenovationCode = S(api, "das_code_nosazi"),
            PlanInstructionNo = S(api, "das_shahrdari_num"),
            PlanInstructionType = S(api, "dastyp_title"),
            PlanInstructionDate = S(api, "das_date_tarkhis"),
            LandArea = S(api, "das_masahat"),
            ParafArea = S(api, "db_metraj_shahrdari"),
            CapacityArea = S(api, "db_metraj_effective_nezarat"),
            StructureType = S(api, "sazetyp_title"),
            BlockTitle = S(api, "db_title"),
            BlockCount = S(api, "das_blocknum"),
            Floors = S(api, "db_tabagha_shahrdari"),
            Units = S(api, "db_vahed_count"),
            Issuer = S(api, "das_sad_title"),
            PermitNumber = S(api, "das_parvana_num"),
            PermitDate = S(api, "das_parvana_date"),
            ReleaseDate = S(api, "das_date_tarkhis"),
            PlanZone = S(api, "mah_title"),
            Address = S(api, "das_address"),
            UsageType = S(api, "usetyp_title"),
        };
    }

    public static List<Engineer> MapEngineers(List<Dictionary<string, object?>> apiList)
    {
        return apiList.Select(e => new Engineer(
            S(e, "mas_title"),
            $"{S(e, "ozh_name")} {S(e, "ozh_famil")}".Trim(),
            ""
        )).ToList();
    }

    public static List<Fee> MapFees(List<Dictionary<string, object?>> apiList)
    {
        return apiList.Select(f =>
        {
            var amount = S(f, "dbm_mablagh");
            var payed = S(f, "payed");
            var stage = S(f, "dbm_marhala_id");
            var paymentDate = S(f, "pardakht_date");
            var docNum = S(f, "sanad_num");
            var docDate = S(f, "sanad_date");

            return new Fee(
                S(f, "mas_title"),                           // Discipline
                "",                                           // ServiceType
                stage,                                        // Stage
                docDate,                                      // StartDate (doc date)
                paymentDate,                                  // EndDate (payment date)
                $"{amount} ریال",                             // Amount
                payed == "yes" ? "پرداخت شده" : payed == "ok" ? "تایید شده" : "پرداخت نشده", // PayStatus
                docNum,                                       // ConfirmStatus (doc num)
                S(f, "dbm_p_type")                            // AmountType
            );
        }).ToList();
    }

    /// <summary>Map API reports to existing ReportRecord model.</summary>
    public static List<ReportRecord> MapReports(List<Dictionary<string, object?>> apiList)
    {
        int rowNum = 0;
        return apiList.Select(r =>
        {
            rowNum++;
            var hasFile = !string.IsNullOrEmpty(S(r, "image_name"));
            return new ReportRecord(
                rowNum.ToString(),                           // RowNo
                S(r, "brt_report_title"),                   // ReportType
                S(r, "brt_marhale_title"),                  // Stage
                $"{S(r, "ozh_name")} {S(r, "ozh_famil")}".Trim(), // Engineer
                S(r, "mas_title"),                           // Discipline
                S(r, "br_bazdid_date"),                      // VisitDate
                S(r, "br_exec_ceil"),                        // CeilingCount
                S(r, "br_andicator"),                        // Indicator
                hasFile                                      // HasFile
            );
        }).ToList();
    }
}
