using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Vitality.Models.DTOs.PrescriptionMedicines;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos;
using Vitality.Models.Repos.Interfaces;

public class PrescriptionMedicinesRepo : BaseRepo, IPrescriptionMedicinesRepo
{
    private static string? TryExtractDirection(string? supplyDescJson)
    {
        if (string.IsNullOrWhiteSpace(supplyDescJson))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(supplyDescJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (doc.RootElement.TryGetProperty("direction", out var d1) && d1.ValueKind == JsonValueKind.String)
                    return d1.GetString();
                if (doc.RootElement.TryGetProperty("Direction", out var d2) && d2.ValueKind == JsonValueKind.String)
                    return d2.GetString();
            }
        }
        catch { }
        return null;
    }

    private static readonly JsonSerializerOptions SupplyJsonOptions = new JsonSerializerOptions
    {

        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };
    public bool Create(long patientPrescriptionId, string medicineName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(medicineName)) return false;

            var entity = new PT_PrescriptionMedicine
            {
                PatientPrescriptionId = patientPrescriptionId,
                MedicineName = medicineName.Trim()
            };

            _db.PT_PrescriptionMedicines.Add(entity);
            _db.SaveChanges();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool CreateMany(long patientPrescriptionId, List<PrescriptionMedicinesRequestDTO> medicines)
    {
        try
        {
            if (medicines == null || medicines.Count == 0) return false;

            var cleaned = medicines
                .Where(m => m != null && !string.IsNullOrWhiteSpace(m.MedicineName))
                .Select(m =>
                {
                    m.PatientPrescriptionId = patientPrescriptionId;
                    m.MedicineName = m.MedicineName.Trim();
                    return m;
                })
                .ToList();

            var existing = _db.PT_PrescriptionMedicines
                              .Where(x => x.PatientPrescriptionId == patientPrescriptionId)
                              .ToList();

            var existingById = existing.ToDictionary(x => x.PrescriptionMedicineId, x => x);
            var incomingIds = new HashSet<long>(cleaned.Where(m => m.PrescriptionMedicineId > 0)
                                                       .Select(m => m.PrescriptionMedicineId));

            var drugIds = cleaned
                .Where(m => m.DrugId.HasValue && m.DrugId.Value > 0)
                .Select(m => m.DrugId!.Value)
                .Distinct()
                .ToList();

            var ptgAll = drugIds.Count > 0
                ? _db.PC_PHARMTOGLOBALs
                    .AsNoTracking()
                    .Where(p => p.IsActive == true && p.DrugId.HasValue && drugIds.Contains(p.DrugId.Value))
                    .ToList()
                : new List<PC_PHARMTOGLOBAL>();

            var latestPtgByDrugId = ptgAll
                .GroupBy(p => p.DrugId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.PharmToGlobalId).First());

            var drugIsCustomByDrugId = drugIds.Count > 0
                ? _db.PD_Drugs
                    .AsNoTracking()
                    .Where(d => drugIds.Contains(d.DrugId))
                    .ToDictionary(d => d.DrugId, d => d.IsCustom)
                : new Dictionary<long, bool?>();

            var upserted = new List<(PT_PrescriptionMedicine entity, PrescriptionMedicinesRequestDTO dto, decimal? calculatedTotalAmount)>();

            foreach (var m in cleaned)
            {

                decimal? medicineQuantity = !string.IsNullOrWhiteSpace(m.Quantity) && decimal.TryParse(m.Quantity, out var qty) ? qty : null;

                decimal? effectiveWholesalePrice = m.WholesalePrice;
                bool? effectiveIsCustom = m.IsCustom;
                if (effectiveIsCustom == null && m.DrugId.HasValue && m.DrugId.Value > 0 && drugIsCustomByDrugId.TryGetValue(m.DrugId.Value, out var dbIsCustom))
                    effectiveIsCustom = dbIsCustom;

                if (m.DrugId.HasValue && m.DrugId.Value > 0)
                {
                    if (effectiveIsCustom == true || effectiveWholesalePrice == null)
                    {
                        if (latestPtgByDrugId.TryGetValue(m.DrugId.Value, out var ptg) && ptg.WholesalePrice.HasValue)
                            effectiveWholesalePrice = ptg.WholesalePrice;
                    }
                }

                decimal? medicineTotalAmount = effectiveWholesalePrice.HasValue && medicineQuantity.HasValue
                    ? effectiveWholesalePrice.Value * medicineQuantity.Value
                    : null;

                if (m.PrescriptionMedicineId > 0 && existingById.TryGetValue(m.PrescriptionMedicineId, out var entity))
                {

                    entity.MedicineName = m.MedicineName;
                    entity.DrugId = m.DrugId;
                    entity.CatalogId = m.CatalogId;
                    entity.DaysSupplies = m.DaysSupplies;
                    entity.Injection = m.Injection;
                    entity.InjectionQuantity = m.InjectionQuantity;
                    entity.Needle = m.Needle;
                    entity.NeedleQuantity = m.NeedleQuantity;
                    entity.Direction = m.Direction;
                    entity.Instruction = m.Instruction;
                    entity.Quantity = m.Quantity;
                    entity.ItemDesignatorID = m.ItemDesignatorID;
                    entity.strenght = m.strenght;
                    entity.DosageForm = m.DosageForm;
                    entity.PackageSize = m.PackageSize;
                    entity.ControlSubstance = m.ControlSubstance;
                    entity.CourierMethod = m.CourierMethod;
                    entity.WholesalePrice = effectiveWholesalePrice;
                    entity.TotalAmount = medicineTotalAmount;

                    var isActiveProp = typeof(PT_PrescriptionMedicine).GetProperty("IsActive");
                    if (isActiveProp != null) isActiveProp.SetValue(entity, true);

                    upserted.Add((entity, m, medicineTotalAmount));
                }
                else
                {

                    var toAdd = new PT_PrescriptionMedicine
                    {
                        PatientPrescriptionId = patientPrescriptionId,
                        MedicineName = m.MedicineName,
                        DrugId = m.DrugId,
                        CatalogId = m.CatalogId,
                        DaysSupplies = m.DaysSupplies,
                        Injection = m.Injection,
                        InjectionQuantity = m.InjectionQuantity,
                        Needle = m.Needle,
                        NeedleQuantity = m.NeedleQuantity,
                        Direction = m.Direction,
                        Instruction = m.Instruction,
                        Quantity = m.Quantity,
                        ItemDesignatorID = m.ItemDesignatorID,
                        strenght = m.strenght,
                        DosageForm = m.DosageForm,
                        PackageSize = m.PackageSize,
                        ControlSubstance = m.ControlSubstance,
                        CourierMethod = m.CourierMethod,
                        WholesalePrice = effectiveWholesalePrice,
                        TotalAmount = medicineTotalAmount
                    };

                    var isActiveProp = typeof(PT_PrescriptionMedicine).GetProperty("IsActive");
                    if (isActiveProp != null) isActiveProp.SetValue(toAdd, true);

                    _db.PT_PrescriptionMedicines.Add(toAdd);
                    upserted.Add((toAdd, m, medicineTotalAmount));
                }
            }

            var removedMeds = existing.Where(x => !incomingIds.Contains(x.PrescriptionMedicineId)).ToList();
            foreach (var old in removedMeds)
            {

                var oldSupplies = _db.Set<PT_MedicineSupply>()
                                     .Where(s => s.PrescriptionMedicineId == old.PrescriptionMedicineId);
                _db.RemoveRange(oldSupplies);

                var isActiveProp = typeof(PT_PrescriptionMedicine).GetProperty("IsActive");
                if (isActiveProp != null)
                    isActiveProp.SetValue(old, false);
                else
                    _db.PT_PrescriptionMedicines.Remove(old);
            }

            _db.SaveChanges();

            decimal grandTotal = 0m;

            foreach (var (entity, dto, calculatedTotalAmount) in upserted)
            {
                var medId = entity.PrescriptionMedicineId;

                if (calculatedTotalAmount.HasValue)
                {
                    grandTotal += calculatedTotalAmount.Value;
                }

                var existingSupplies = _db.Set<PT_MedicineSupply>()
                                          .Where(s => s.PrescriptionMedicineId == medId);
                _db.RemoveRange(existingSupplies);

                if (dto.Supplies != null && dto.Supplies.Count > 0)
                {
                    var toInsert = dto.Supplies.Select(s =>
                    {

                        decimal? supplyQuantity = !string.IsNullOrWhiteSpace(s.SupplyQuantity) && decimal.TryParse(s.SupplyQuantity, out var qty) ? qty : null;
                        decimal? supplyTotalAmount = s.WholesalePrice.HasValue && supplyQuantity.HasValue
                            ? s.WholesalePrice.Value * supplyQuantity.Value
                            : null;

                        if (supplyTotalAmount.HasValue)
                        {
                            grandTotal += supplyTotalAmount.Value;
                        }

                        return new PT_MedicineSupply
                        {
                            PrescriptionMedicineId = medId,
                            SupplyDesc = s.SupplyDesc.ValueKind == JsonValueKind.Undefined
                                                        ? null
                                                        : s.SupplyDesc.GetRawText(),
                            SupplyQuantity = s.SupplyQuantity,
                            Name = s.Name,
                            Direction = s.Direction,
                            SupplyItemDesignatorID = s.SupplyItemDesignatorID,
                            WholesalePrice = s.WholesalePrice,
                            TotalAmount = supplyTotalAmount
                        };
                    }).ToList();

                    _db.AddRange(toInsert);
                }
            }

            _db.SaveChanges();

            try
            {
                var prescription = _db.PT_PatientPrescriptions
                    .FirstOrDefault(p => p.PatientPrescriptionId == patientPrescriptionId);

                if (prescription != null && prescription.PatientOrderId.HasValue)
                {
                    var patientOrder = _db.PT_PatientOrders
                        .FirstOrDefault(o => o.PatientOrderId == prescription.PatientOrderId.Value);

                    if (patientOrder != null)
                    {
                        patientOrder.OrderTotal = grandTotal;
                        _db.SaveChanges();
                    }
                }
            }
            catch
            {

            }

            return true;
        }
        catch
        {
            return false;
        }
    }
    public List<PrescriptionMedicineItemDTO> GetMedicinesByPrescriptionId(long patientPrescriptionId)
    {

        var meds = _db.PT_PrescriptionMedicines
            .AsNoTracking()
            .Where(m => m.PatientPrescriptionId == patientPrescriptionId)
            .OrderBy(m => m.PrescriptionMedicineId)
            .ToList();

        if (meds.Count == 0) return new List<PrescriptionMedicineItemDTO>();

        var catalogIds = meds
            .Where(m => m.CatalogId.HasValue && m.CatalogId.Value > 0)
            .Select(m => m.CatalogId!.Value)
            .Distinct()
            .ToList();
        var catalogNamesById = catalogIds.Count > 0
            ? _db.PD_Catalogs.AsNoTracking()
                .Where(c => c.IsActive == true && catalogIds.Contains(c.CatalogId))
                .ToDictionary(c => c.CatalogId, c => c.CatalogName)
            : new Dictionary<long, string?>();

        var medIds = meds.Select(m => m.PrescriptionMedicineId).ToList();

        var supplies = _db.Set<PT_MedicineSupply>()
            .AsNoTracking()
            .Where(s => medIds.Contains(s.PrescriptionMedicineId))
            .OrderBy(s => s.MedicineSupplyId)
            .ToList();

        var suppliesByMedId = supplies
            .GroupBy(s => s.PrescriptionMedicineId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(s => new MedicineSupplyDTO
                {
                    MedicineSupplyId = s.MedicineSupplyId,
                    PrescriptionMedicineId = s.PrescriptionMedicineId,
                    SupplyDesc = s.SupplyDesc,
                    Name = s.Name,
                    Direction = !string.IsNullOrWhiteSpace(s.Direction) ? s.Direction : TryExtractDirection(s.SupplyDesc),
                    SupplyQuantity = s.SupplyQuantity,
                    SupplyItemDesignatorID = s.SupplyItemDesignatorID,
                    WholesalePrice = s.WholesalePrice,
                    TotalAmount = s.TotalAmount
                }).ToList()
            );

        var result = meds.Select(m => new PrescriptionMedicineItemDTO
        {
            PrescriptionMedicineId = m.PrescriptionMedicineId,
            PatientPrescriptionId = m.PatientPrescriptionId,
            MedicineName = m.MedicineName ?? string.Empty,
            DrugId = m.DrugId,
            CatalogId = m.CatalogId,
            CatalogName = (m.CatalogId.HasValue && catalogNamesById.TryGetValue(m.CatalogId.Value, out var catName)) ? catName : null,
            DaysSupplies = m.DaysSupplies,
            Injection = m.Injection,
            InjectionQuantity = m.InjectionQuantity,
            Needle = m.Needle,
            NeedleQuantity = m.NeedleQuantity,
            Direction = m.Direction,
            Instruction = m.Instruction,
            Quantity = m.Quantity,

            ItemDesignatorID = m.ItemDesignatorID,
            strenght = m.strenght,
            DosageForm = m.DosageForm,
            PackageSize = m.PackageSize,
            ControlSubstance = m.ControlSubstance,
            CourierMethod = m.CourierMethod,
            WholesalePrice = m.WholesalePrice,
            TotalAmount = m.TotalAmount,

            Supplies = suppliesByMedId.TryGetValue(m.PrescriptionMedicineId, out var list)
                                        ? list
                                        : new List<MedicineSupplyDTO>()
        })
        .ToList();

        return result;
    }

    public PrescriptionMedicineItemDTO? GetById(long prescriptionMedicineId)
    {

        var m = _db.PT_PrescriptionMedicines
            .AsNoTracking()
            .FirstOrDefault(x => x.PrescriptionMedicineId == prescriptionMedicineId);

        if (m == null) return null;

        string? catalogName = null;
        if (m.CatalogId.HasValue && m.CatalogId.Value > 0)
        {
            catalogName = _db.PD_Catalogs.AsNoTracking()
                .Where(c => c.CatalogId == m.CatalogId.Value && c.IsActive == true)
                .Select(c => c.CatalogName)
                .FirstOrDefault();
        }

        var supplies = _db.Set<PT_MedicineSupply>()
            .AsNoTracking()
            .Where(s => s.PrescriptionMedicineId == prescriptionMedicineId)
            .OrderBy(s => s.MedicineSupplyId)
            .Select(s => new MedicineSupplyDTO
            {
                MedicineSupplyId = s.MedicineSupplyId,
                PrescriptionMedicineId = s.PrescriptionMedicineId,
                SupplyDesc = s.SupplyDesc,
                Direction = !string.IsNullOrWhiteSpace(s.Direction) ? s.Direction : TryExtractDirection(s.SupplyDesc),
                SupplyQuantity = s.SupplyQuantity,
                SupplyItemDesignatorID = s.SupplyItemDesignatorID,
                Name = s.Name,
                WholesalePrice = s.WholesalePrice,
                TotalAmount = s.TotalAmount
            })
            .ToList();

        return new PrescriptionMedicineItemDTO
        {
            PrescriptionMedicineId = m.PrescriptionMedicineId,
            PatientPrescriptionId = m.PatientPrescriptionId,
            MedicineName = m.MedicineName ?? string.Empty,
            DrugId = m.DrugId,
            CatalogId = m.CatalogId,
            CatalogName = catalogName,
            DaysSupplies = m.DaysSupplies,
            Injection = m.Injection,
            InjectionQuantity = m.InjectionQuantity,
            Needle = m.Needle,
            NeedleQuantity = m.NeedleQuantity,
            Direction = m.Direction,
            Instruction = m.Instruction,
            Quantity = m.Quantity,

            ItemDesignatorID = m.ItemDesignatorID,
            strenght = m.strenght,
            DosageForm = m.DosageForm,
            PackageSize = m.PackageSize,
            ControlSubstance = m.ControlSubstance,
            CourierMethod = m.CourierMethod,
            WholesalePrice = m.WholesalePrice,
            TotalAmount = m.TotalAmount,

            Supplies = supplies
        };
    }

    public bool UpdateName(long prescriptionMedicineId, string medicineName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(medicineName))
                return false;

            var entity = _db.PT_PrescriptionMedicines
                            .FirstOrDefault(x => x.PrescriptionMedicineId == prescriptionMedicineId);
            if (entity == null) return false;

            entity.MedicineName = medicineName.Trim();

            _db.SaveChanges();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool UpdateMedicine(PrescriptionMedicinesRequestDTO dto)
    {
        try
        {
            var entity = _db.PT_PrescriptionMedicines
                            .FirstOrDefault(x => x.PrescriptionMedicineId == dto.PrescriptionMedicineId);
            if (entity == null) return false;

            if (string.IsNullOrWhiteSpace(dto.MedicineName)) return false;

            entity.MedicineName = dto.MedicineName.Trim();
            entity.DrugId = dto.DrugId;
            entity.CatalogId = dto.CatalogId;
            entity.DaysSupplies = dto.DaysSupplies;
            entity.Injection = dto.Injection;
            entity.InjectionQuantity = dto.InjectionQuantity;
            entity.Needle = dto.Needle;
            entity.NeedleQuantity = dto.NeedleQuantity;
            entity.Direction = dto.Direction;
            entity.Instruction = dto.Instruction;
            entity.ItemDesignatorID = dto.ItemDesignatorID;
            entity.Quantity = dto.Quantity;

            _db.SaveChanges();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool Delete(long prescriptionMedicineId)
    {
        try
        {
            var entity = _db.PT_PrescriptionMedicines
                            .FirstOrDefault(x => x.PrescriptionMedicineId == prescriptionMedicineId);
            if (entity == null) return false;

            _db.PT_PrescriptionMedicines.Remove(entity);
            _db.SaveChanges();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
