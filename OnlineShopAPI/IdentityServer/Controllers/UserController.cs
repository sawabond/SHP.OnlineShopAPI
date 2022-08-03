﻿using AutoMapper;
using DAL.Entities;
using DAL.Interfaces;
using IdentityServer.DTO;
using IdentityServer.Extensions;
using IdentityServer.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace IdentityServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly IMapper _mapper;
        private readonly ITokenService _tokenService;
        private readonly IUnitOfWork _uow;

        public UserController(
            IMapper mapper,
            ITokenService tokenService,
            IUnitOfWork uow
            )
        {
            _mapper = mapper;
            _tokenService = tokenService;
            _uow = uow;
        }

        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> RegisterUser([FromBody] UserRegisterDto userRegisterDto)
        {
            var existingUser = await _uow.UserRepository.GetUserByUsernameAsync(userRegisterDto.UserName);

            if (existingUser != null)
            {
                return BadRequest("User with this name already exists");
            }

            var newUser = _mapper.Map<AppUser>(userRegisterDto);

            var result = await _uow.UserRepository.CreateUserAsync(newUser, userRegisterDto.Password);

            if (!result.Succeeded)
            {
                return BadRequest(result.ToErrorsString());
            }

            //var createdUser = await _uow.UserRepository.GetUserByUsernameAsync(userRegisterDto.UserName);

            await _uow.UserRepository.AddToRoleAsync(newUser, "buyer");

            var roles = await _uow.UserRepository.GetUserRoles(newUser); //changed

            var userDto = _mapper.Map<UserDto>(newUser); //changed
            var token = _tokenService.CreateToken(newUser, roles); //changed
            userDto.Token = token;

            return Ok(userDto);
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> LogIn([FromBody] UserLogInDto userLogInDto)
        {
            var user = await _uow.UserRepository.GetUserByUsernameAsync(userLogInDto.UserName);

            if (user == null)
            {
                return Unauthorized($"There is not user with username {userLogInDto.UserName}");
            }

            var result = await _uow.SignInManager
                .CheckPasswordSignInAsync(user, userLogInDto.Password ?? string.Empty, lockoutOnFailure: false);

            if (!result.Succeeded)
            {
                return Unauthorized("Wrong password");
            }

            var roles = await _uow.UserRepository.GetUserRoles(user);
            var userDto = _mapper.Map<UserDto>(user);
            userDto.Token = _tokenService.CreateToken(user, roles);

            return Ok(userDto);
        }

        [HttpPost("google-register")]
        public async Task<ActionResult<UserDto>> GoogleRegisterUser(string token)
        {
            var dto = _tokenService.GetOAuthDtoFromToken(token);

            var user = await _uow.UserRepository.GetUserByUsernameAsync(dto.UserName);

            UserDto userDto;

            if (user != null)
            {
                return BadRequest("User with this name already exists");
            }

            var newUser = _mapper.Map<AppUser>(dto);

            var result = await _uow.UserRepository.CreateUserAsync(newUser, "pas$worD123456789426734683275235382");

            if (!result.Succeeded)
            {
                return BadRequest(result.ToErrorsString());
            }

            await _uow.UserRepository.AddToRoleAsync(newUser, "buyer");

            var roles = await _uow.UserRepository.GetUserRoles(newUser);

            userDto = _mapper.Map<UserDto>(newUser);
            var newToken = _tokenService.CreateToken(newUser, roles);
            userDto.Token = newToken;

            return Ok(userDto);
        }

        [HttpPost("google-login")]
        public async Task<ActionResult<UserDto>> GoogleLoginUser(string token)
        {
            var dto = _tokenService.GetOAuthDtoFromToken(token);

            var user = await _uow.UserRepository.GetUserByUsernameAsync(dto.UserName);

            UserDto userDto;

            if (user == null)
            {
                return Unauthorized($"There is not user with username {dto.UserName}");
            }

            var roles = await _uow.UserRepository.GetUserRoles(user);
            userDto = _mapper.Map<UserDto>(user);
            userDto.Token = _tokenService.CreateToken(user, roles);

            return Ok(userDto);
        }
    }
}
