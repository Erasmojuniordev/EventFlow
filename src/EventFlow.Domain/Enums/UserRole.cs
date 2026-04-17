namespace EventFlow.Domain.Enums;

/// <summary>
/// Papéis de usuário no sistema.
///
/// Usamos strings constantes para os roles no Identity (não enum diretamente),
/// mas definimos aqui como constantes centralizadas para evitar magic strings
/// espalhadas pelo código.
///
/// Por que não usar enum diretamente com [Authorize(Roles = UserRole.Organizer)]?
/// - Atributos em C# só aceitam constantes em tempo de compilação
/// - Enum.ToString() em atributos não funciona — precisaria de conversão manual
/// - Strings constantes são mais simples e amplamente usadas no Identity
///
/// Como usar:
///   [Authorize(Roles = UserRole.Organizer)]
///   services.AddRoles([UserRole.Admin, UserRole.Organizer, UserRole.Attendee])
/// </summary>
public static class UserRole
{
    public const string Admin = "Admin";
    public const string Organizer = "Organizer";
    public const string Attendee = "Attendee";

    // Array útil para seed e registro de roles no Identity
    public static readonly string[] All = [Admin, Organizer, Attendee];
}
