using AutoMapper;
using DudeMeds.Models.DTOs.Products;
using DudeMeds.Models.DTOs.Tickets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vitality.Models.DTOs.Brands;
using Vitality.Models.EntityClasses;
using Vitality.Models.Repos.Interfaces;

namespace Vitality.Models.Repos.Services
{
    public class BrandsRepo : BaseRepo , IBrandsRepo
    {
        private readonly IMapper _mapper;
        public BrandsRepo(IMapper mapper)
        {
            _mapper = mapper;
        }
        public GetBrandByIdResponseDTO GetBrandById(string? FacilityGuid)
        {
            GetBrandByIdResponseDTO response = new GetBrandByIdResponseDTO();
            SYS_Facility facility = _db.SYS_Facilities.Where(x => x.Guid == FacilityGuid).FirstOrDefault();
            SYS_Brand brand = _db.SYS_Brands.Where(x => x.FacilityId == facility.FacilityId).FirstOrDefault();
            response = _mapper.Map<GetBrandByIdResponseDTO>(brand);
            response.PrimaryColorVariants = _db.BD_PrimaryColorVarients.Where(x => x.BrandId == brand.BrandId)
           .Select(x => x.PrimaryColorVarient).ToList();
            response.ChartColors = _db.BD_ChartColors.Where(x => x.BrandId == brand.BrandId)
          .Select(x => x.ChartColor).ToList();
            return response;
        }

        public bool SaveBrand(SaveBrandRequestDTO request, long UserId)
        {
            try
            {
                SYS_Brand brand = new SYS_Brand();
                if (request.BrandId == 0)
                {
                    brand = _mapper.Map<SYS_Brand>(request);
                    brand.CreatedBy = UserId;
                    brand.CreatedDate = DateTime.UtcNow;
                    brand.IsActive = true;
                    _db.SYS_Brands.Add(brand);
                    _db.SaveChanges();

                    foreach (var PrimaryColorVarient in request.PrimaryColorVariants)
                    {
                        BD_PrimaryColorVarient newItem = new BD_PrimaryColorVarient
                        {
                            PrimaryColorVarientId = 0,
                            BrandId = brand.BrandId,
                            PrimaryColorVarient = PrimaryColorVarient,
                        };
                        _db.BD_PrimaryColorVarients.Add(newItem);
                    }
                    _db.SaveChanges();

                    foreach (var chart in request.ChartColors)
                    {
                        BD_ChartColor newItem = new BD_ChartColor
                        {
                           ChartColorId = 0,
                           BrandId = brand.BrandId,
                           ChartColor = chart,
                        };
                        _db.BD_ChartColors.Add(newItem);
                    }
                    _db.SaveChanges();
                }
                else
                {
                    brand = _db.SYS_Brands.Where(x => x.BrandId == request.BrandId).FirstOrDefault();
                    _mapper.Map(request, brand);

                    brand.ModifiedBy = UserId;
                    brand.ModifiedDate = DateTime.UtcNow;
                    _db.SaveChanges();

                    List<BD_PrimaryColorVarient> list1 = new List<BD_PrimaryColorVarient>();
                    list1 = _db.BD_PrimaryColorVarients.Where(x => x.BrandId == request.BrandId).ToList();
                    if (list1.Count != 0)
                    {
                        _db.BD_PrimaryColorVarients.RemoveRange(list1);
                        _db.SaveChanges();
                    }
                    foreach (var PrimaryColorVarient in request.PrimaryColorVariants)
                    {
                        BD_PrimaryColorVarient newItem = new BD_PrimaryColorVarient
                        {
                            PrimaryColorVarientId = 0,
                            BrandId = brand.BrandId,
                            PrimaryColorVarient = PrimaryColorVarient,
                        };
                        _db.BD_PrimaryColorVarients.Add(newItem);
                    }
                    _db.SaveChanges();

                    List<BD_ChartColor> list2 = new List<BD_ChartColor>();
                    list2 = _db.BD_ChartColors.Where(x => x.BrandId == request.BrandId).ToList();
                    if (list2.Count != 0)
                    {
                        _db.BD_ChartColors.RemoveRange(list2);
                        _db.SaveChanges();
                    }
                    foreach (var chart in request.ChartColors)
                    {
                        BD_ChartColor newItem = new BD_ChartColor
                        {
                            ChartColorId = 0,
                            BrandId = brand.BrandId,
                            ChartColor = chart,
                        };
                        _db.BD_ChartColors.Add(newItem);
                    }
                    _db.SaveChanges();
                }
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
