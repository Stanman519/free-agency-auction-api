using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using FreeAgencyAuctionAPI.Models;
using FreeAgencyAuctionAPI.Repos;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StreamChat.Clients;
using StreamChat.Models;

namespace FreeAgencyAuctionAPI.Services
{
    public interface IOwnerService
    {
        public Task UpdateCapSpaceForOwners(List<int> capSpace, int leagueId);
        Task<OwnerDTO> SynchronizeAuthorizedUser(AuthUser user);
        Task<OwnerDTO> GetOwnerDTOByAuthUserSub(string userSub);
        public Task<List<OpposingFranchiseDTO>> GetAllOwners(int leaugeId);
        public Task<OwnerDTO> Login(OwnerDTO owner);
        Task<OwnerDTO> CookieLogin(string login);
        Task<OwnerDTO> Register(OwnerDTO newUser);
        Task CreateTestLeague();

        //Task SendWinningMessageToChat(string name, int salary, int years, string ownername);
    }
    public class OwnerService : IOwnerService
    {
        private readonly IMapper _mapper;
        private readonly IOwnerRepo _repo;
        private readonly IMessageClient _messageClient;
        private readonly IUserClient _userClient;
        private readonly IMflApi _mfl;
        private readonly IGMBot _gm;

        public OwnerService(IMapper mapper, IOwnerRepo repo, IMessageClient messageClient, IUserClient userClient, IMflApi mfl, IGMBot gm)
        {
            _mapper = mapper;
            _repo = repo;
            _messageClient = messageClient;
            _userClient = userClient;
            _mfl = mfl;
            _gm = gm;
        }
        public async Task UpdateCapSpaceForOwners(List<int> capSpace, int leagueId)
        {
            await _repo.UpdateCapRoomForAllOwners(capSpace, leagueId);

        }


        public async Task<List<OpposingFranchiseDTO>> GetAllOwners(int leagueId)
        {
            var ret = await _repo.GetAllOwners(leagueId);

            var owners = ret.Select(_ => new OpposingFranchiseDTO
            {
                CapRoom = _.Caproom ?? 0,
                YearsLeft = _.Yearsleft ?? 0,
                Mflfranchiseid = _.Mflfranchiseid,
                Leagueownerid = _.Leagueownerid,
                TeamName = _.Teamname,
                OwnerName = _.Owner.Displayname,
                Avatar = _.Owner.Avatar
            }).ToList();
            // owners.ForEach(o =>
            // {
            //     o.TipsUsed = allTips.FirstOrDefault(t => t.Key == o.OwnerId)?.Select(t => t).ToList();
            // });
            return owners;
        }

        public async Task<OwnerDTO> GetOwnerDTOByAuthUserSub(string userSub)
        {
            var dto = await _repo.GetOwnerByAuthId(userSub);
            dto.StreamToken = await GetStreamToken(dto);
            return dto;
        }


        public async Task<OwnerDTO> SynchronizeAuthorizedUser(AuthUser user)
        {
            // check db first for owner, with this userid, return it or create one if it doesnt exist.
            var dto = await _repo.GetOwnerByAuthId(user.Sub);
            // With addition of games, how do we skip this process?? new screen i guess.
            if (dto == null || dto.OwnerId < 1) 
            {
                var matchingFranchises = new List<Franchise>();
                /*var leagues = await _repo.GetAllRealLeagueIds();
                foreach (var league in leagues)
                {
                    var root = await _mfl.GetBigLeagueObject(league);
                    var franchises = root.league.franchises.franchise;
                    var foundFranchise = franchises.FirstOrDefault(franchise =>
                    {
                        return franchise.email?.ToLower() == user.Email?.ToLower() ||
                            franchise.username?.ToLower() == user.Nickname?.ToLower() ||
                            franchise.username?.ToLower() == user.PreferredUsername?.ToLower() ||
                            franchise.owner_name?.ToLower() == user.Name?.ToLower();
                    });
                    if (foundFranchise != null)
                    {
                        foundFranchise.leagueId = league;
                        matchingFranchises.Add(foundFranchise);
                    }          
                }*/
                dto = await _repo.AddOwnerAndRelatedLeagues(user, matchingFranchises);
            }
            dto.StreamToken = await GetStreamToken(dto);
            return dto;
        }




        public async Task<OwnerDTO> Login(OwnerDTO owner)
        {
            var plaintextBytes= System.Text.Encoding.UTF8.GetBytes(owner.Password);
            owner.Password = Convert.ToBase64String(plaintextBytes);

           
            var dbOwner =  await _repo.Login(owner);
            dbOwner.StreamToken = _userClient.CreateToken(dbOwner.Ownername);
            return dbOwner;
        }

        //saving to db doesn't work?
        public async Task<string> GetStreamToken(OwnerDTO owner)
        {

            try
            {
                return _userClient.CreateToken($"{owner.OwnerId}");
            }
            catch (Exception e)
            {
                await _gm.NotifyMflError(new BotMessage($"stream error: {e.Message}", string.Empty));
            }
            return "";
            //await _repo.UpdateOwnerStreamToken(owner, token);
/*            owner.StreamToken = token;
            return owner;*/
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
            if (dbOwner != null) dbOwner.StreamToken = _userClient.CreateToken(dbOwner.Ownername);

            return dbOwner;
        }

        public async Task<OwnerDTO> Register(OwnerDTO newUser)
        {
            var plaintextBytes= System.Text.Encoding.UTF8.GetBytes(newUser.Password);
            newUser.Password = System.Convert.ToBase64String(plaintextBytes);
            
            var entity = _mapper.Map<OwnerDTO, OwnerEntity>(newUser);
            entity.Premium = false;
            var dbOwner = _mapper.Map<OwnerEntity, OwnerDTO>(await _repo.Register(entity));

            dbOwner.StreamToken = _userClient.CreateToken(dbOwner.Ownername);
            return dbOwner;
        }

        public async Task CreateTestLeague()
        {
            await _repo.MakeTestLeague();
        }
        //TODO: THESE GM MESSAGES NEED TO BE LEAGUE SPECIFIC!
        //This should bee somewheere else but the client needs to be wired up in startup and I'm doing this during the auction
        /*        public async Task SendWinningMessageToChat(string name, int salary, int years, string ownername)
                { 
                    await _messageClient.SendMessageAsync("messaging","chat", "cap",$"{ownername} acquired {name} at ${salary}, {years} years.");
                }*/
    }
}