import * as signalR from "@microsoft/signalr";
import "./css/main.css";

const divMessages: HTMLDivElement = document.querySelector("#divMessages");
const tbMessage: HTMLInputElement = document.querySelector("#tbMessage");
const btnSend: HTMLButtonElement = document.querySelector("#btnSend");
const username = new Date().getTime();

/* The HubConnectionBuilder class creates a new builder for configuring the server connection.
   The withUrl function configures the hub URL. */
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hub")
    .build();

// registers the lambda function that will be invoked when the hub method from the server is invoked
connection.on("messageReceived", (username: string, message: string) => {
  /* a new div element is created with the author's name and the message content in its innerHTML attribute.
     it's then added to the main div element displaying the messages */
  const m = document.createElement("div");

  m.innerHTML = `<div class="message-author">${username}</div><div>${message}</div>`;

  divMessages.appendChild(m);
  divMessages.scrollTop = divMessages.scrollHeight;
});

connection.start().catch((err) => document.write(err));

/* Fires when the user types in the tbMessage textbox and
   calls the send function when the user presses the Enter key.*/
tbMessage.addEventListener("keyup", (e: KeyboardEvent) => {
  if (e.key === "Enter") {
    send();
  }
});

/* Fires when the user clicks the Send button and calls the send function. */
btnSend.addEventListener("click", send);

function send() {
  connection.send("newMessage", username, tbMessage.value)
      .then(() => (tbMessage.value = ""));
}
