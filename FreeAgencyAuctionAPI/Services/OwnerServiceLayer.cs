using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using Microsoft.Extensions.Options;
using StreamChat.Clients;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IOwnerServiceLayer
    {
        //public Task UpdateCapSpaceForOwners(List<int> capSpace);
        public Task<List<OwnerDTO>> GetAllOwners();
        public Task<OwnerDTO> Login(OwnerDTO owner);
        Task<OwnerDTO> CookieLogin(string login);
        Task<OwnerDTO> Register(OwnerDTO newUser);
    }
    public class OwnerServiceLayer : IOwnerServiceLayer
    {
        private readonly IMapper _mapper;
        private readonly IOwnerRepo _repo;
        private StreamClientFactory _factory;

        public OwnerServiceLayer(IMapper mapper, IOwnerRepo repo, IOptionsSnapshot<AppConfig> options)
        {
            _mapper = mapper;
            _repo = repo;
            _factory = new StreamClientFactory(options.Value.Stream.Key, options.Value.Stream.Password);
        }
/*        public async Task UpdateCapSpaceForOwners(List<int> capSpace)
        {
            //await _repo.UpdateCapRoomForAllOwners(leagueId, capSpace);

        }*/

        public async Task<List<OwnerDTO>> GetAllOwners()
        {
            var ret = await _repo.GetAllOwners();
            return _mapper.Map<List<OwnerEntity>, List<OwnerDTO>>(ret);
        }

        public async Task<OwnerDTO> Login(OwnerDTO owner)
        {
            var plaintextBytes= System.Text.Encoding.UTF8.GetBytes(owner.Password);
            owner.Password = Convert.ToBase64String(plaintextBytes);

            var dbOwner =  await _repo.Login(owner);
            var userClient = _factory.GetUserClient();
            dbOwner.StreamToken = userClient.CreateToken(dbOwner.Ownername);
            return dbOwner;
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
            var dbOwner =  await _repo.Login(loginAttempt);
            var userClient = _factory.GetUserClient();
            dbOwner.StreamToken = userClient.CreateToken(dbOwner.Ownername);
            return dbOwner;
        }

        public async Task<OwnerDTO> Register(OwnerDTO newUser)
        {
            var plaintextBytes= System.Text.Encoding.UTF8.GetBytes(newUser.Password);
            newUser.Password = System.Convert.ToBase64String(plaintextBytes);
            
            var entity = _mapper.Map<OwnerDTO, OwnerEntity>(newUser);
            entity.Premium = false;
            var dbOwner = _mapper.Map<OwnerEntity, OwnerDTO>(await _repo.Register(entity));
            var userClient = _factory.GetUserClient();
            dbOwner.StreamToken = userClient.CreateToken(dbOwner.Ownername);
            return dbOwner;
        }
        
    }
}