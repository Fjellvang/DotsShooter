// This file is part of Metaplay SDK which is released under the Metaplay SDK License.
// Globally useful Bootstrap-Vue components.
// Note: The goal is to remove all of these as soon as possible in favour of MetaUiNext.
import {
  ButtonPlugin,
  LayoutPlugin,
  AlertPlugin,
  CardPlugin,
  SpinnerPlugin,
  ListGroupPlugin,
  TablePlugin,
  ModalPlugin,
  LinkPlugin,
  ImagePlugin,
  SkeletonPlugin,
  AvatarPlugin,
} from 'bootstrap-vue'
// Luxon global settings.
import { Settings } from 'luxon'
import { createPinia } from 'pinia'
import type { App } from 'vue'

// Metaplay components and libraries.
import '@metaplay/bootstrap'
import MetaUiPlugin from '@metaplay/meta-ui'
import { useNotificationsVuePlugin } from '@metaplay/meta-ui-next'

// Font awesome icons.
import { library } from '@fortawesome/fontawesome-svg-core'
import { faCircle as farCircle, faWindowClose, faCaretSquareRight } from '@fortawesome/free-regular-svg-icons'
import {
  faTachometerAlt,
  faUsers,
  faUser,
  faSignOutAlt,
  faSignInAlt,
  faPaperPlane,
  faBroadcastTower,
  faCommentAlt,
  faCog,
  faCogs,
  faCode,
  faCircle,
  faClipboardList,
  faAngleDoubleLeft,
  faHamburger,
  faLock,
  faPlusSquare,
  faCalendarAlt,
  faTimes,
  faClone,
  faFileDownload,
  faTrashAlt,
  faCloud,
  faExternalLinkAlt,
  faBug,
  faTable,
  faUserTag,
  faTags,
  faChess,
  faChessRook,
  faSearch,
  faAngleRight,
  faSyncAlt,
  faSortAmountDown,
  faSortAmountUp,
  faInbox,
  faBoxArchive,
  faKey,
  faDatabase,
  faAmbulance,
  faMoneyCheckAlt,
  faClock,
  faBusinessTime,
  faQuestionCircle,
  faExclamationTriangle,
  faPaperclip,
  faSlidersH,
  faList,
  faPeopleArrows,
  faExchangeAlt,
  faLanguage,
  faIdCard,
  faTabletAlt,
  faChartLine,
  faFlask,
  faBinoculars,
  faChartBar,
  faCoins,
  faArrowRight,
  faArrowLeft,
  faArrowDown,
  faTag,
  faWrench,
  faBackward,
  faFastBackward,
  faForward,
  faFastForward,
  faUserAstronaut,
  faInfoCircle,
  faLockOpen,
  faChessKnight,
  faCodeBranch,
  faIndustry,
  faRightLeft,
  faAngleDown,
  faCopy,
  faCheck,
  faXmark,
  faShieldHalved,
  faCubes,
  faCube,
  faTrophy,
  faDesktop,
  faBellSlash,
  faRobot,
  faCircleExclamation,
  faScaleUnbalanced,
  faArrowDownLong,
  faArrowTurnUp,
  faArrowRightLong,
  faMinus,
  faPlus,
} from '@fortawesome/free-solid-svg-icons'
import { FontAwesomeIcon } from '@fortawesome/vue-fontawesome'

import { router } from './index'

// ----- Actual init code starts here -----
export function CorePlugin(app: App): void {
  // Set Luxon locale to en-gb for consistent date formatting.
  Settings.defaultLocale = 'en-gb'
  // Settings.defaultZone = 'utc' TODO: is this safe to set? Any implications to existing customer code?

  // Register a global error handlers.
  useNotificationsVuePlugin(app)

  // Initialize Pinia
  const pinia = createPinia()
  app.use(pinia)

  // Core router
  app.use(router)

  // Global UI components
  app.use(MetaUiPlugin)

  // Bootstrap-Vue
  app.use(ButtonPlugin)
  app.use(LayoutPlugin)
  app.use(AlertPlugin)
  app.use(CardPlugin)
  app.use(SpinnerPlugin)
  app.use(ListGroupPlugin)
  app.use(TablePlugin)
  app.use(ModalPlugin)
  app.use(LinkPlugin)
  app.use(ImagePlugin)
  app.use(SkeletonPlugin)
  app.use(AvatarPlugin)

  // Font Awesome
  library.add(
    faTachometerAlt,
    faUsers,
    faUser,
    faSignOutAlt,
    faSignInAlt,
    faPaperPlane,
    faBroadcastTower,
    faCommentAlt,
    faCog,
    faCogs,
    faCode,
    faCircle,
    farCircle,
    faClipboardList,
    faAngleDoubleLeft,
    faHamburger,
    faLock,
    faPlusSquare,
    faCalendarAlt,
    faTimes,
    faClone,
    faFileDownload,
    faTrashAlt,
    faWindowClose,
    faCloud,
    faExternalLinkAlt,
    faBug,
    faTable,
    faUserTag,
    faTags,
    faChess,
    faChessRook,
    faSearch,
    faAngleRight,
    faSyncAlt,
    faSortAmountDown,
    faSortAmountUp,
    faInbox,
    faBoxArchive,
    faKey,
    faDatabase,
    faAmbulance,
    faMoneyCheckAlt,
    faClock,
    faBusinessTime,
    faQuestionCircle,
    faExclamationTriangle,
    faPaperclip,
    faSlidersH,
    faList,
    faPeopleArrows,
    faExchangeAlt,
    faLanguage,
    faIdCard,
    faTabletAlt,
    faChartLine,
    faFlask,
    faBinoculars,
    faChartBar,
    faCoins,
    faArrowRight,
    faArrowLeft,
    faArrowDown,
    faTag,
    faWrench,
    faBackward,
    faFastBackward,
    faForward,
    faFastForward,
    faUserAstronaut,
    faInfoCircle,
    faLockOpen,
    faChessKnight,
    faCodeBranch,
    faIndustry,
    faCaretSquareRight,
    faRightLeft,
    faAngleDown,
    faCopy,
    faCheck,
    faXmark,
    faShieldHalved,
    faCubes,
    faCube,
    faTrophy,
    faDesktop,
    faBellSlash,
    faRobot,
    faCircleExclamation,
    faScaleUnbalanced,
    faArrowDownLong,
    faArrowTurnUp,
    faArrowRightLong,
    faMinus,
    faPlus
  )
  app.component('FaIcon', FontAwesomeIcon)
}
