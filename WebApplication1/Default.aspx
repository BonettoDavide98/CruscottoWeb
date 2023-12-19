<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="CruscottoWeb.Default"%>

<!DOCTYPE html>

<html>
<head>
	<style type="text/css">
		.row {
			overflow: auto;
			display: flex;
		}
		.cam {
			flex: 1;
			display: inherit;
			margin-left: auto;
			margin-right: auto;
			max-width: 45%;
			height: auto;
		}
		.button_grey {
			font-size: 16px;
			margin: 10px;
			padding: 10px;
			color: white;
			background-color: #606060;
		}
		.button_grey_selected {
			background-color: blue;
		}
		.tab > * {
			grid-row-start: 1;
			grid-row-end: 2;
		}
		.tab_text_center > * > * {
			text-align: center;
			font-size: 20px;
			color: white;
			background-color: #606060;
			border: solid;
			margin: 5px;
		}
		#Form1 {
			display: block;
		}
		#menu > * {
			margin: 10px;
		}
		html {
			background-color: #202020;
		}
		* {
			font-family: Arial;
		}
	</style>
    <script src="Scripts/jquery-3.3.1.min.js"></script>
    <script src="Scripts/jquery.signalR-2.2.2.min.js"></script>
    <script src="signalr/hubs"></script>
    <title></title>
</head>
<body>
	<div id="menu" style="position: absolute; bottom: 0; right: 0; border: solid; display: none; background-color: white; justify-items: center;">
		<span style="grid-row: 1; grid-column-start: 1; grid-column-end: 3;">RICETTE</span>
	</div>
	<form id="Form1">
		<div id="header" style="overflow: auto;">
			<div id="menu_homepage" style="float: left">
				<img src="Images/home_icon.png" style="max-height: 8vh; padding: 20px 0;" />
			</div>
			<div id="camselection" style="float: left; margin: 20px;">
			</div>
			<div id="logo_header">
				<img src="Images/logovettoriale_qualivision.png" style="max-height: 5vh; float: right" />
			</div>
		</div>
		<div id="statistiche" class="tab tab_text_center" style="display: grid; column-gap:10px;">
			<div id="tot">
				<p>TOT</p>
				<p id="tot_counter">0</p>
			</div>
			<div id="ok">
				<p style="color: green; background-color: #202020;">OK</p>
				<p id="ok_counter">0</p>
			</div>
			<div id="ko">
				<p style="color: red; background-color: #202020;">KO</p>
				<p id="ko_counter">0</p>
			</div>
		</div>
		<div class="row" id="cams" style="margin: 25px; border: solid; border-color: white;" runat="server">
		</div>
		<div id="tools">
			<input class="button_grey" type="button" id="stopbutton" value="STOP" />
			<input class="button_grey" type="button" id="startbutton" value="START" />
			<input class="textField1" type="text" id="parametername" placeholder="Nome parametro" />
			<input class="textField1" type="text" id="value" placeholder="Valore" />
			<input class="button_grey" type="button" id="setbutton" value="SET" />
			<input class="button_grey" type="button" id="ricettebutton" value="RICETTE" onclick="ToggleDivRicette()"/>
		</div>

		<script type="text/javascript">
            $(function () {
                //instanzia connessione con hub
                var updater = $.connection.hubMessageQueue;

                //mostra/nasconde il div di selezione ricetta
				document.getElementById("ricettebutton").onclick = function () {
					var divricette = document.getElementById("menu");
					if(divricette.style.display == "grid")
						divricette.style.display = "none";
					else
						divricette.style.display = "grid";
                };

                //rimuove ogni telecamera e pagina
                updater.client.ResetSettings = function () {
                    var cams = document.getElementById("cams");
                    while (cams.childElementCount > 0)
                        cams.removeChild(cams.firstChild);

                    var pages = document.getElementById("camselection");
                    while (pages.childElementCount > 0)
                        pages.removeChild(pages.firstChild);
                    
                    var params = document.getElementById("menu");
                    while (params.childElementCount > 1)
                        params.removeChild(params.lastChild);
                };

                //aggiorna l'immagine visualizzata dalla telecamera camNumber per simulare una live
				updater.client.UpdateImage = function (camNumber, base64Data) {
					document.getElementById("LiveImg" + camNumber.toString()).src = base64Data;
				};

                //aggiorna i contatori TOT, OK e KO
				updater.client.UpdateStats = function (tot, ok, ko) {
					document.getElementById("tot_counter").innerHTML = tot;
					document.getElementById("ok_counter").innerHTML = ok;
					document.getElementById("ko_counter").innerHTML = ko;
				};

                //nasconde la telecamera camNumber
                updater.client.HideCam = function (camNumber) {
                    document.getElementById("LiveImg" + camNumber.toString()).style.display = "none";
                };

                //mostra la telecamera camNumber
                updater.client.ShowCam = function (camNumber) {
                    document.getElementById("LiveImg" + camNumber.toString()).style.display = "inherit";
                };

                //crea i bottoni della selezione pagine
                updater.client.SetPages = function (pages) {
                    var node = document.getElementById("camselection");

                    for (let i = 1; i <= pages; i++) {
                        var button = document.createElement("input");

                        button.className = "button_grey";
                        if (i == 1) {
                            button.className += " button_grey_selected";
                        }
                        button.setAttribute("type", "button");
                        button.value = i.toString();
                        button.id = "buttonCam" + i.toString();

                        //aggiunge ad ogni bottone la funzione di highlight dell'ultimo bottone cliccato
                        button.onclick = function () {
                            updater.server.changePage(this.value);
                            var buttons = document.getElementById("camselection").childNodes;
                            for (let j = 0; j < buttons.length; j++) {
                                try {
                                    buttons[j].classList.remove("button_grey_selected");
                                } catch { }
                            }
                            this.classList.add("button_grey_selected");
                        };

                        node.appendChild(button);
                    }
                };

                //crea un img per ogni telecamera che si vuole mostrare contemporaneamente
                //il loro attributo src viene aggiornato costantemente dalla funzione precedente UpdateImage
                updater.client.SetCams = function (cams) {
                    var node = document.getElementById("cams");

                    for (let i = 1; i <= cams; i++) {
                        var img = document.createElement("img");

                        img.className = "cam";
                        img.id = "LiveImg" + i.toString();
                        img.src = "Images/Offline_Img.bmp";

                        node.appendChild(img);
                    }
                };

                //aggiunge il parametro specificato
                //TODO: bozza
                updater.client.AddParameter = function (parameterName) {
                    var menu = document.getElementById("menu");

                    var desc = document.createElement("span");
                    desc.innerHTML = "RICETTA " + ((menu.childElementCount + 1) / 2).toString();
                    desc.style = "grid-row: " + ((menu.childElementCount + 2) / 2).toString() + "; grid-column: 1;";

                    var item = document.createElement("span");
                    item.innerHTML = parameterName;
                    item.style = "grid-row: " + ((menu.childElementCount + 2) / 2).toString() + "; grid-column: 2;";

                    menu.appendChild(desc);
                    menu.appendChild(item);
                };

                //avvia la connessione e assegna funzioni aggiuntive
				$.connection.hub.start().done(function () {
					updater.server.startMessageQueue();

					$('#stopbutton').click(function () {
						updater.server.stop();
					});

					$('#startbutton').click(function () {
						updater.server.start();
					});

					$('#setbutton').click(function () {
						updater.server.set($('#parametername').val(), $('#value').val());
					});
				});
			});
		</script>
	</form>
</body>
</html>
