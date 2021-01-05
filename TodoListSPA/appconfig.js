// Configuration of the Azure AD Application for this TodoList Single Page application
// Note that changing popUp to false will produce a completely different UX based on redirects instead of popups.
var config = {
    tenant: "ms0604.onmicrosoft.com",
    clientId: "0ca9db6e-ca5a-4948-b032-bc4b46792f71",
    redirectUri: "http://localhost:16969/",
    popUp: true
}

// Configuration of the Azure AD Application for the WebAPI called by this single page application (TodoListService)
var webApiConfig = {
    resourceId: "https://ms0604.onmicrosoft.com/TodoListService-OBO-CA",
    resourceBaseAddress: "https://localhost:44321/",
}
