using EscapeHub.Core.DTOs;

namespace EscapeHub.Web.Models;

public sealed record RoomGalleryImage(string Caption, string AltText, string? ImagePath = null, string? SvgId = null);

public sealed record RoomPageModel(RoomDto Room, IReadOnlyList<RoomGalleryImage> Images);
