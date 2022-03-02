using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IOwnerServiceLayer
    {
        public Task UpdateCapSpaceForOwners(List<int> capSpace);
        public Task<List<OwnerDTO>> GetAllOwners();
        public Task<OwnerDTO> Login(OwnerDTO owner);
        Task<OwnerDTO> CookieLogin(string login);
        Task<OwnerDTO> Register(OwnerDTO newUser);
    }
    public class OwnerServiceLayer : IOwnerServiceLayer
    {
        private readonly IMapper _mapper;
        private readonly IOwnerRepo _repo;

        public OwnerServiceLayer(IMapper mapper, IOwnerRepo repo)
        {
            _mapper = mapper;
            _repo = repo;
        }
        public async Task UpdateCapSpaceForOwners(List<int> capSpace)
        {
            await _repo.UpdateCapRoomForAllOwners(capSpace);

        }

        public async Task<List<OwnerDTO>> GetAllOwners()
        {
            var ret = await _repo.GetAllOwners();
            return _mapper.Map<List<OwnerEntity>, List<OwnerDTO>>(ret);
        }

        public async Task<OwnerDTO> Login(OwnerDTO owner)
        {
            var plaintextBytes= System.Text.Encoding.UTF8.GetBytes(owner.Password);
            owner.Password = System.Convert.ToBase64String(plaintextBytes);
            return await _repo.Login(owner);
        }

        public async Task<OwnerDTO> CookieLogin(string login)
        {
            Console.WriteLine(login);
            var ownerArr = login.Split(",");
            var loginAttempt = new OwnerDTO
            {
                Ownername = ownerArr[0],
                Password = ownerArr[1]
            };
            return  await _repo.Login(loginAttempt);
        }

        public async Task<OwnerDTO> Register(OwnerDTO newUser)
        {
            var plaintextBytes= System.Text.Encoding.UTF8.GetBytes(newUser.Password);
            newUser.Password = System.Convert.ToBase64String(plaintextBytes);
            var entity = _mapper.Map<OwnerDTO, OwnerEntity>(newUser);
            return _mapper.Map<OwnerEntity, OwnerDTO>(await _repo.Register(entity));
        }
        
    }
}