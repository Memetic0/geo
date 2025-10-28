import Aurelia from 'aurelia';
import { RouterConfiguration } from '@aurelia/router';
import { App } from './shell/app';
import 'leaflet/dist/leaflet.css';

// Import modular stylesheets
import './styles/variables.css';
import './styles/base.css';
import './styles/navigation.css';
import './styles/buttons.css';
import './styles/forms.css';
import './styles/filters.css';
import './styles/modals.css';
import './styles/dashboard.css';
import './styles/incident-table.css';
import './styles/map.css';

Aurelia
  .register(RouterConfiguration)
  .app(App)
  .start();
