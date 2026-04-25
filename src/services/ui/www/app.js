function base() {
  if (window.WEATHER_API_BASE) return String(window.WEATHER_API_BASE).replace(/;+$/, "");
  return "";
}
async function j(url) {
  const r = await fetch(url, { headers: { Accept: "application/json" } });
  if (!r.ok) throw new Error(await r.text());
  return r.json();
}
document.getElementById("load").onclick = async () => {
  const lat = document.getElementById("lat").value;
  const lon = document.getElementById("lon").value;
  const b = base();
  const d = await j(`${b}/api/weather/current?lat=${encodeURIComponent(lat)}&lon=${encodeURIComponent(lon)}`);
  document.getElementById("cur").textContent = JSON.stringify(d, null, 2);
};
document.getElementById("save").onclick = async () => {
  const lat = parseFloat(document.getElementById("lat").value);
  const lon = parseFloat(document.getElementById("lon").value);
  const b = base();
  const d = new Date();
  const r = await fetch(`${b}/api/weather/snapshots`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ lat, lon, label: d.toISOString().slice(0, 10) }),
  });
  if (!r.ok) throw new Error(await r.text());
  document.getElementById("list").click();
};
document.getElementById("list").onclick = async () => {
  const b = base();
  const rows = await j(`${b}/api/weather/snapshots`);
  const ol = document.getElementById("shots");
  ol.innerHTML = "";
  for (const s of rows) {
    const li = document.createElement("li");
    li.textContent = `${s.savedAtUtc}  ${s.label}  T=${s.temperatureC}°C  code=${s.weatherCode}  (${s.latitude}, ${s.longitude})`;
    li.title = JSON.stringify(s.raw, null, 0);
    ol.appendChild(li);
  }
};
