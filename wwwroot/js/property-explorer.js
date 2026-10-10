(() => {
  const explorer = document.querySelector("[data-property-explorer]");
  const mapElement = document.getElementById("property-map");
  const dataElement = document.getElementById("property-map-data");
  const suggestionDataElement = document.getElementById("property-search-suggestion-data");
  if (!explorer || !mapElement || !dataElement || !window.L) return;

  const properties = JSON.parse(dataElement.textContent || "[]");
  const form = explorer.querySelector("[data-explorer-form]");
  const bboxInput = explorer.querySelector("[data-bbox-input]");
  const zoomInput = explorer.querySelector("[data-zoom-input]");
  const viewInput = explorer.querySelector("[data-view-input]");
  const searchAreaButton = explorer.querySelector("[data-search-area]");
  const locationSearch = explorer.querySelector("[data-location-search]");
  const suggestionList = explorer.querySelector("[data-search-suggestions]");
  const visibleCount = explorer.querySelector("[data-visible-count]");
  const resultNoun = explorer.querySelector("[data-result-noun]");
  const unmappedNote = explorer.querySelector("[data-unmapped-note]");
  const viewportEmpty = explorer.querySelector("[data-viewport-empty]");
  const viewButtons = [...explorer.querySelectorAll("[data-view-button]")];
  const cards = new Map(
    [...explorer.querySelectorAll("[data-property-id]")].map((card) => [Number(card.dataset.propertyId), card])
  );
  const markers = new Map();

  const suggestions = suggestionDataElement
    ? JSON.parse(suggestionDataElement.textContent || "[]")
    : [];
  let activeSuggestion = -1;

  const closeSuggestions = () => {
    if (!locationSearch || !suggestionList) return;
    suggestionList.hidden = true;
    suggestionList.replaceChildren();
    locationSearch.setAttribute("aria-expanded", "false");
    locationSearch.removeAttribute("aria-activedescendant");
    activeSuggestion = -1;
  };

  const chooseSuggestion = (value) => {
    if (!locationSearch) return;
    locationSearch.value = value;
    bboxInput.value = "";
    zoomInput.value = "";
    closeSuggestions();
    locationSearch.focus();
  };

  const renderSuggestions = () => {
    if (!locationSearch || !suggestionList) return;
    const query = locationSearch.value.trim().toLocaleLowerCase();
    if (!query) {
      closeSuggestions();
      return;
    }

    const matches = suggestions
      .filter((suggestion) => suggestion.value.toLocaleLowerCase().includes(query))
      .sort((left, right) => {
        const leftStarts = left.value.toLocaleLowerCase().startsWith(query) ? 0 : 1;
        const rightStarts = right.value.toLocaleLowerCase().startsWith(query) ? 0 : 1;
        return leftStarts - rightStarts || left.type.localeCompare(right.type) || left.value.localeCompare(right.value);
      })
      .slice(0, 8);

    suggestionList.replaceChildren();
    activeSuggestion = -1;
    matches.forEach((suggestion, index) => {
      const option = document.createElement("button");
      option.type = "button";
      option.id = `property-search-suggestion-${index}`;
      option.className = "pe-suggestion";
      option.setAttribute("role", "option");
      option.setAttribute("aria-selected", "false");

      const value = document.createElement("span");
      value.textContent = suggestion.value;
      const type = document.createElement("small");
      type.textContent = suggestion.type;
      option.append(value, type);
      option.addEventListener("pointerdown", (event) => event.preventDefault());
      option.addEventListener("click", () => chooseSuggestion(suggestion.value));
      suggestionList.append(option);
    });

    suggestionList.hidden = matches.length === 0;
    locationSearch.setAttribute("aria-expanded", String(matches.length > 0));
  };

  const setActiveSuggestion = (nextIndex) => {
    if (!locationSearch || !suggestionList) return;
    const options = [...suggestionList.querySelectorAll("[role='option']")];
    if (!options.length) return;
    activeSuggestion = (nextIndex + options.length) % options.length;
    options.forEach((option, index) => {
      const selected = index === activeSuggestion;
      option.setAttribute("aria-selected", String(selected));
      option.classList.toggle("is-active", selected);
    });
    locationSearch.setAttribute("aria-activedescendant", options[activeSuggestion].id);
    options[activeSuggestion].scrollIntoView({ block: "nearest" });
  };

  if (locationSearch && suggestionList) {
    locationSearch.addEventListener("input", () => {
      bboxInput.value = "";
      zoomInput.value = "";
      renderSuggestions();
    });
    locationSearch.addEventListener("focus", renderSuggestions);
    locationSearch.addEventListener("blur", () => window.setTimeout(closeSuggestions, 0));
    locationSearch.addEventListener("keydown", (event) => {
      if (event.key === "ArrowDown") {
        event.preventDefault();
        setActiveSuggestion(activeSuggestion + 1);
      } else if (event.key === "ArrowUp") {
        event.preventDefault();
        setActiveSuggestion(activeSuggestion - 1);
      } else if (event.key === "Enter" && activeSuggestion >= 0) {
        event.preventDefault();
        const option = suggestionList.querySelectorAll("[role='option']")[activeSuggestion];
        if (option) chooseSuggestion(option.querySelector("span").textContent);
      } else if (event.key === "Escape") {
        closeSuggestions();
      }
    });
  }

  const syncListToMapBounds = () => {
    const bounds = map.getBounds();
    let count = 0;

    cards.forEach((card, id) => {
      const marker = markers.get(id);
      const isVisible = Boolean(marker && bounds.contains(marker.getLatLng()));
      card.hidden = !isVisible;
      if (isVisible) count += 1;
    });

    if (visibleCount) visibleCount.textContent = String(count);
    if (resultNoun) resultNoun.textContent = count === 1 ? "property in this map area" : "properties in this map area";
    if (unmappedNote) unmappedNote.hidden = true;
    if (viewportEmpty) viewportEmpty.hidden = count !== 0;
  };

  const map = L.map(mapElement, { zoomControl: true, scrollWheelZoom: false });
  L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
    maxZoom: 19,
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
  }).addTo(map);

  const clusters = L.markerClusterGroup({
    disableClusteringAtZoom: 14,
    spiderfyOnMaxZoom: true,
    showCoverageOnHover: false,
    maxClusterRadius: 52
  });

  const formatPrice = (property) => {
    if (!property.price) return "View";
    const currency = (property.currency || "GBP").toUpperCase();
    try {
      return new Intl.NumberFormat(undefined, {
        style: "currency",
        currency,
        maximumFractionDigits: 0
      }).format(property.price);
    } catch {
      return `${property.price} ${currency}`;
    }
  };

  const selectProperty = (id, source) => {
    explorer.querySelectorAll(".is-selected").forEach((element) => element.classList.remove("is-selected"));
    const card = cards.get(id);
    const marker = markers.get(id);
    if (card) {
      card.classList.add("is-selected");
      if (source === "marker") {
        card.scrollIntoView({ behavior: "smooth", block: "nearest" });
        card.focus({ preventScroll: true });
      }
    }
    if (marker) marker.getElement()?.classList.add("is-selected");
  };

  properties.forEach((property) => {
    const markerLabel = document.createElement("span");
    markerLabel.className = "pe-marker";
    markerLabel.textContent = formatPrice(property);
    const icon = L.divIcon({
      className: `pe-marker-wrap${property.approximate ? " is-approximate" : ""}`,
      html: markerLabel,
      iconSize: [72, 34],
      iconAnchor: [36, 17]
    });
    const marker = L.marker([property.latitude, property.longitude], {
      icon,
      title: `${property.name}, ${property.location}${property.approximate ? " (approximate location)" : ""}`
    });
    marker.on("click", () => selectProperty(property.id, "marker"));
    marker.bindTooltip(property.name, { direction: "top", offset: [0, -18] });
    markers.set(property.id, marker);
    clusters.addLayer(marker);
  });
  map.addLayer(clusters);

  const initialBounds = (explorer.dataset.initialBbox || "")
    .split(",")
    .map(Number);
  if (initialBounds.length === 4 && initialBounds.every(Number.isFinite)) {
    map.fitBounds([
      [initialBounds[1], initialBounds[0]],
      [initialBounds[3], initialBounds[2]]
    ]);
  } else if (properties.length) {
    const bounds = L.latLngBounds(properties.map((property) => [property.latitude, property.longitude]));
    map.fitBounds(bounds, { padding: [42, 42], maxZoom: 13 });
  } else {
    map.setView([54.8, -4.1], 6);
  }

  let readyForMoves = false;
  let suppressMoveEnd = false;
  map.whenReady(() => window.setTimeout(() => { readyForMoves = true; }, 0));
  map.on("moveend", () => {
    if (readyForMoves && !suppressMoveEnd) {
      syncListToMapBounds();
      searchAreaButton.hidden = false;
    }
  });

  searchAreaButton.addEventListener("click", () => {
    const bounds = map.getBounds();
    bboxInput.value = [bounds.getWest(), bounds.getSouth(), bounds.getEast(), bounds.getNorth()]
      .map((value) => value.toFixed(5))
      .join(",");
    zoomInput.value = String(map.getZoom());
    form.requestSubmit();
  });

  cards.forEach((card, id) => {
    card.addEventListener("mouseenter", () => selectProperty(id, "card"));
    card.addEventListener("focusin", () => selectProperty(id, "card"));
    card.addEventListener("click", () => {
      const marker = markers.get(id);
      if (!marker) return;
      clusters.zoomToShowLayer(marker, () => {
        map.panTo(marker.getLatLng());
        selectProperty(id, "card");
      });
    });
  });

  const setView = (view, replaceHistory = true) => {
    const normalized = view === "map" ? "map" : "list";
    explorer.dataset.activeView = normalized;
    viewInput.value = normalized;
    viewButtons.forEach((button) => button.setAttribute("aria-pressed", String(button.dataset.viewButton === normalized)));
    if (normalized === "map") {
      readyForMoves = false;
      suppressMoveEnd = true;
      searchAreaButton.hidden = true;
      window.setTimeout(() => {
        map.invalidateSize();
        window.setTimeout(() => {
          suppressMoveEnd = false;
          readyForMoves = true;
        }, 0);
      }, 0);
    }
    if (replaceHistory) {
      const url = new URL(window.location.href);
      url.searchParams.set("view", normalized);
      window.history.replaceState({}, "", url);
    }
  };

  viewButtons.forEach((button) => button.addEventListener("click", () => setView(button.dataset.viewButton)));
  setView(explorer.dataset.initialView, false);
})();
