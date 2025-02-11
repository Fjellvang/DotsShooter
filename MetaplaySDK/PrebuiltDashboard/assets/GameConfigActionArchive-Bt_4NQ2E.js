import{d as B,f as C,x as S,c as A,ad as y,M as V,bN as W,O as x,ae as _,a9 as O,b as z,o as n,a as c,e as b,w as d,n as a,p as r,t as f,k as u,l as H,F as N,$ as P,_ as U}from"./index-B7jvAjdE.js";import{M as T}from"./MActionModal-coQLkvWs.js";import{M as j}from"./MInputSwitch-C-Z-JJsG.js";import{g as k}from"./gameConfigs-DlpfRS5r.js";const D=B({__name:"GameConfigActionArchive",props:{gameConfigId:{},triggerStyle:{}},setup(p,{expose:i}){i();const s=y(),e=p,h=C(!1),w=C(),{data:o,refresh:g}=S(k()),G=A(()=>{if(o.value)return o.value.find(l=>l.id===e.gameConfigId)}),m=A(()=>{const l=o.value?.find(t=>t.id===e.gameConfigId);return l?(o.value??[]).filter(t=>!t.isArchived&&!(t.publishedAt!==null||t.unpublishedAt!==null||t.isActive)&&t.buildStartedAt<l.buildStartedAt).map(t=>t.id):[]}),{showSuccessNotification:v}=_();async function I(){const l={isArchived:!G.value?.isArchived};await s.post(`/gameConfig/${e.gameConfigId}`,l),h.value&&m.value.length>0?(await s.post("/gameConfig/archive",m.value),v(`Archived ${m.value.length+1} game configs.`)):v(`Game config ${l.isArchived?"archived":"unarchived"}.`),g()}const M={gameServerApi:s,props:e,archiveOlderConfigsState:h,gameConfigArchiveModal:w,allGameConfigsData:o,allGameConfigsRefresh:g,singleGameConfigWithoutContents:G,archivableGameConfigIds:m,showSuccessNotification:v,onOk:I,computed:A,ref:C,get useGameServerApi(){return y},get MActionModal(){return T},get MBadge(){return V},get MIconButton(){return W},get MInputSwitch(){return j},get MButton(){return x},get useNotifications(){return _},get maybePluralString(){return O},get useSubscription(){return S},get getAllGameConfigsSubscriptionOptions(){return k}};return Object.defineProperty(M,"__isScriptSetup",{enumerable:!1,value:!0}),M}}),F={key:1,class:"tw-inline-flex tw-h-4 tw-w-5",xmlns:"http://www.w3.org/2000/svg",fill:"currentColor",viewBox:"0 0 640 512"},Y={key:0},E={key:1},L={key:2,class:P(["tw-mt-4 tw-border tw-rounded-md tw-py-2 tw-px-3 tw-border-neutral-200 tw-bg-neutral-100 tw-text-neutral-600"])},R={class:"tw-flex tw-justify-between"},q={class:"tw-font-semibold"},J={class:"small text-muted"};function K(p,i,s,e,h,w){const o=z("fa-icon");return n(),c(N,null,[s.triggerStyle==="icon"?(n(),b(e.MIconButton,{key:0,permission:"api.game_config.edit","aria-label":e.singleGameConfigWithoutContents?.isArchived?"Unarchive this game config":"Archive this game config.","disabled-tooltip":e.singleGameConfigWithoutContents?.isActive?"Cannot archive the active game config.":void 0,onClick:e.gameConfigArchiveModal?.open,"data-testid":"`archive-config-${gameConfigId}`"},{default:d(()=>[e.singleGameConfigWithoutContents?.isArchived?(n(),c("svg",F,i[2]||(i[2]=[a("path",{d:"M256 48c0-26.5 21.5-48 48-48H592c26.5 0 48 21.5 48 48V464c0 26.5-21.5 48-48 48H381.3c1.8-5 2.7-10.4 2.7-16V253.3c18.6-6.6 32-24.4 32-45.3V176c0-26.5-21.5-48-48-48H256V48zM571.3 347.3c6.2-6.2 6.2-16.4 0-22.6l-64-64c-6.2-6.2-16.4-6.2-22.6 0l-64 64c-6.2 6.2-6.2 16.4 0 22.6s16.4 6.2 22.6 0L480 310.6V432c0 8.8 7.2 16 16 16s16-7.2 16-16V310.6l36.7 36.7c6.2 6.2 16.4 6.2 22.6 0zM0 176c0-8.8 7.2-16 16-16H368c8.8 0 16 7.2 16 16v32c0 8.8-7.2 16-16 16H16c-8.8 0-16-7.2-16-16V176zm352 80V480c0 17.7-14.3 32-32 32H64c-17.7 0-32-14.3-32-32V256H352zM144 320c-8.8 0-16 7.2-16 16s7.2 16 16 16h96c8.8 0 16-7.2 16-16s-7.2-16-16-16H144z"},null,-1)]))):(n(),b(o,{key:0,class:"tw-size-3.5",icon:"box-archive"}))],void 0,!0),_:1},8,["aria-label","disabled-tooltip","onClick"])):(n(),b(e.MButton,{key:1,permission:"api.game_config.edit","disabled-tooltip":e.singleGameConfigWithoutContents?.isActive?"Cannot archive the active game config.":void 0,"full-width":"",onClick:i[0]||(i[0]=g=>e.gameConfigArchiveModal?.open())},{default:d(()=>[r(f(e.singleGameConfigWithoutContents?.isArchived?"Unarchive":"Archive"),1)],void 0,!0),_:1},8,["disabled-tooltip"])),u(e.MActionModal,{ref:"gameConfigArchiveModal",title:e.singleGameConfigWithoutContents?.isArchived?"Unarchive Game Config":"Archive Game Config",action:e.onOk,"ok-button-label":e.singleGameConfigWithoutContents?.isArchived?"Unarchive":"Archive","data-testid":"`archive-config-${gameConfigId}`"},{default:d(()=>[e.singleGameConfigWithoutContents?.isArchived?(n(),c("div",E,[a("span",null,[i[6]||(i[6]=r("You are about to unarchive the game config ")),u(e.MBadge,null,{default:d(()=>[r(f(e.singleGameConfigWithoutContents?.name),1)],void 0,!0),_:1}),i[7]||(i[7]=r(". "))]),i[8]||(i[8]=a("span",null,"This will make the game config visible in the list of available game configs again.",-1))])):(n(),c("div",Y,[a("span",null,[i[3]||(i[3]=r("You are about to archive the game config ")),u(e.MBadge,null,{default:d(()=>[r(f(e.singleGameConfigWithoutContents?.name),1)],void 0,!0),_:1}),i[4]||(i[4]=r(". "))]),i[5]||(i[5]=a("span",null,"Archiving a game config will hide it from the list of available game configs. An archived game config can be unarchived at any time.",-1))])),!e.singleGameConfigWithoutContents?.isArchived&&e.archivableGameConfigIds?.length>0?(n(),c("div",L,[a("div",R,[a("div",q,"Also archive "+f(e.maybePluralString(e.archivableGameConfigIds?.length,"older game config")),1),u(e.MInputSwitch,{"model-value":e.archiveOlderConfigsState,name:"archiveAllOlderState",size:"small","onUpdate:modelValue":i[1]||(i[1]=g=>e.archiveOlderConfigsState=g),"data-testid":"config-archive-all-older-toggle"},null,8,["model-value"])]),a("div",J,"At the same time as archiving this game config, you can also automatically archive "+f(e.maybePluralString(e.archivableGameConfigIds?.length,"older, unpublished game config"))+". This is useful in keeping your game config history manageable.",1)])):H("",!0)],void 0,!0),_:1},8,["title","ok-button-label"])],64)}const ee=U(D,[["render",K],["__file","GameConfigActionArchive.vue"]]);export{ee as G};
