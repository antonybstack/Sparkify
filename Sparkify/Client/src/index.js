"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var signalR = require("@microsoft/signalr");
require("./css/main.css");
var divMessages = document.querySelector("#divMessages");
var tbMessage = document.querySelector("#tbMessage");
var btnSend = document.querySelector("#btnSend");
/* The HubConnectionBuilder class creates a new builder for configuring the server connection.
   The withUrl function configures the hub URL. */
var connection = new signalR.HubConnectionBuilder()
    .withUrl("/hub")
    .build();
// registers the lambda function that will be invoked when the hub method name SendMessage() from the server is invoked
connection.on("SendMessage", function (username, message) {
    /* a new div element is created with the author's name and the message content in its innerHTML attribute.
       it's then added to the main div element displaying the messages */
    var m = document.createElement("div");
    m.innerHTML = "<div class=\"message-author\">".concat(username, "</div><div>").concat(message, "</div>");
    divMessages.appendChild(m);
    divMessages.scrollTop = divMessages.scrollHeight;
});
connection.start().catch(function (err) { return document.write(err); });
/* Fires when the user types in the tbMessage textbox and
   calls the send function when the user presses the Enter key.*/
tbMessage.addEventListener("keyup", function (e) {
    if (e.key === "Enter") {
        send();
    }
});
/* Fires when the user clicks the Send button and calls the send function. */
btnSend.addEventListener("click", send);
function send() {
    connection.send("SendMessageToGroup", tbMessage.value)
        .then(function () { return (tbMessage.value = ""); });
}
