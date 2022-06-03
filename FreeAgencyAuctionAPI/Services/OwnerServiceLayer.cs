using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using StreamChat.Clients;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IOwnerServiceLayer
    {
        public Task UpdateCapSpaceForOwners(List<int> capSpace);
        public Task<List<OwnerDTO>> GetAllOwners();
        public Task<OwnerDTO> Login(OwnerDTO owner);
        Task<OwnerDTO> CookieLogin(string login);
        Task<OwnerDTO> Register(OwnerDTO newUser);
        Task SendWinningMessageToChat(string name, int salary, int years, string ownername);
    }
    public class OwnerServiceLayer : IOwnerServiceLayer
    {
        private readonly IMapper _mapper;
        private readonly IOwnerRepo _repo;

        private readonly IMessageClient _messageClient;

        private readonly IUserClient _userClient;
        // private StreamClientFactory _factory;


        public OwnerServiceLayer(IMapper mapper, IOwnerRepo repo, IMessageClient messageClient, IUserClient userClient)
        {
            _mapper = mapper;
            _repo = repo;
            _messageClient = messageClient;
            _userClient = userClient;
            // _factory = new StreamClientFactory("REDACTED_STREAM_KEY", "REDACTED_STREAM_SECRET");
            
        }
        public async Task UpdateCapSpaceForOwners(List<int> capSpace)
        {
            await _repo.UpdateCapRoomForAllOwners(capSpace);

        }

        public async Task<List<OwnerDTO>> GetAllOwners()
        {
            var ret = await _repo.GetAllOwners();
            
            var owners = _mapper.Map<List<OwnerEntity>, List<OwnerDTO>>(ret);
            // owners.ForEach(o =>
            // {
            //     o.TipsUsed = allTips.FirstOrDefault(t => t.Key == o.OwnerId)?.Select(t => t).ToList();
            // });
            return owners;
        }

        public async Task<OwnerDTO> Login(OwnerDTO owner)
        {
            var plaintextBytes= System.Text.Encoding.UTF8.GetBytes(owner.Password);
            owner.Password = System.Convert.ToBase64String(plaintextBytes);

           
            var dbOwner =  await _repo.Login(owner);
            if (dbOwner == null) return null;
            dbOwner.TipsUsed = await _repo.GetAllTipsByOwnerId(dbOwner.OwnerId);
            //var userClient = _factory.GetUserClient();
            dbOwner.Token = _userClient.CreateToken(dbOwner.Ownername);
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
            //var userClient = _factory.GetUserClient();
            dbOwner.Token = _userClient.CreateToken(dbOwner.Ownername);
            dbOwner.TipsUsed = await _repo.GetAllTipsByOwnerId(dbOwner.OwnerId);
            return dbOwner;
        }

        public async Task<OwnerDTO> Register(OwnerDTO newUser)
        {
            var plaintextBytes= System.Text.Encoding.UTF8.GetBytes(newUser.Password);
            newUser.Password = System.Convert.ToBase64String(plaintextBytes);
            
            var entity = _mapper.Map<OwnerDTO, OwnerEntity>(newUser);
            entity.premium = false;
            var dbOwner = _mapper.Map<OwnerEntity, OwnerDTO>(await _repo.Register(entity));
            //var userClient = _factory.GetUserClient();
            dbOwner.Token = _userClient.CreateToken(dbOwner.Ownername);
            return dbOwner;
        }
        //This should bee somewheere else but the client needs to be wired up in startup and I'm doing this during the auction
        public async Task SendWinningMessageToChat(string name, int salary, int years, string ownername)
        { 
            await _messageClient.SendMessageAsync("messaging","chat", "cap",$"{ownername} acquired {name} at ${salary}, {years} years.");
        }
    }
}