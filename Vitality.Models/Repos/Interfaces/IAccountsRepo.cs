using DudeMeds.Models.DTOs.Accounts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DudeMeds.Models.Repos.Interfaces
{
    public interface IAccountsRepo
    {
        public LoginResponseDTO Login(LoginRequestDTO request);
        public ChangePasswordResponseDTO ChangePassword(ChangePasswordRequestDTO request);
        public string? GenerateForgotPasswordCode(string Email);
    }
}
