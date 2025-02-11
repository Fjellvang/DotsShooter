import{d as H,x as B,f as S,c as O,ad as R,M as Y,q as j,bN as q,s as K,v as z,ae as D,b as h,o as l,a as u,k as a,w as o,n as f,p as r,t as m,F as J,r as Q,e as F,l as A,_ as U}from"./index-B7jvAjdE.js";import{M as W}from"./MActionModal-coQLkvWs.js";import{p as I}from"./utils-Dwlepb3M.js";import{M as w,a as L,c as b,b as T}from"./metaListUtils-B-xQ0HnD.js";import{a as P}from"./players-CydHenLG.js";import"./index-fVjXqxSF.js";const X=1,Z=H({__name:"PlayerLoginMethodsCard",props:{playerId:{}},setup(k,{expose:t}){t();const g=k,e=R(),{data:d,refresh:M}=B(P(g.playerId)),s=S(null),p=S(),v=["displayString","id"],y=[new w("Attached time ","attachedAt",L.Ascending),new w("Attached time ","attachedAt",L.Descending)],n=[new T("type",[new b("Client token",i=>i.type==="device"),new b("Social auth",i=>i.type==="social")])],c=O(()=>I(d.value.model.attachedAuthMethods,d.value.model.deviceHistory));function V(i){s.value=i,p.value?.open(i)}const{showSuccessNotification:C,showErrorNotification:N}=D();async function E(){const i=s.value.name;try{await e.delete(`/players/${d.value.id}/auths/${s.value.name}/${s.value.id}`);const _=`'${i}' deleted from ${d.value.model.playerName||"n/a"}.`;C(_),M()}catch(_){const G=`Failed to remove '${i}' from ${d.value.model.playerName||"n/a"}. Reason: ${_.response.data.error.details}`;N(G,"Backend Error")}}const x={props:g,gameServerApi:e,playerData:d,playerRefresh:M,authToRemove:s,removeAuthModal:p,searchFields:v,sortOptions:y,defaultSortOption:X,filterSets:n,allAuths:c,onRemoveAuthClick:V,showSuccessNotification:C,showErrorNotification:N,removeAuth:E,computed:O,ref:S,get useGameServerApi(){return R},get parseAuthMethods(){return I},get MetaListFilterOption(){return b},get MetaListFilterSet(){return T},get MetaListSortDirection(){return L},get MetaListSortOption(){return w},get MActionModal(){return W},get MBadge(){return Y},get MCollapse(){return j},get MIconButton(){return q},get MList(){return K},get MListItem(){return z},get useNotifications(){return D},get useSubscription(){return B},get getSinglePlayerSubscriptionOptions(){return P}};return Object.defineProperty(x,"__isScriptSetup",{enumerable:!1,value:!0}),x}}),$={key:0},ee={key:0},te={key:1},oe={class:"tw-space-x-1"},ae={key:0},ne={class:"tw-text-neutral-500"},re={key:1,class:"tw-text-center tw-italic tw-text-neutral-400"},se={class:"tw-mb-2"},ie={key:0,class:"text-danger tw-mb-4 tw-italic"};function le(k,t,g,e,d,M){const s=h("fa-icon"),p=h("meta-time"),v=h("meta-no-seatbelts"),y=h("meta-list-card");return e.playerData?(l(),u("div",$,[a(y,{title:"Login Methods",icon:"key",itemList:e.allAuths,tooltip:"You can move these login methods to connect to another account via the admin actions.",searchFields:e.searchFields,sortOptions:e.sortOptions,defaultSortOption:e.defaultSortOption,filterSets:e.filterSets,emptyMessage:`${e.playerData.model.playerName||"n/a"} has no credentials attached.`,"data-testid":"player-login-methods-card"},{"item-card":o(({item:n})=>[a(e.MCollapse,{extraMListItemMargin:""},{header:o(()=>[a(e.MListItem,{noLeftPadding:""},{"top-right":o(()=>[f("span",oe,[t[2]||(t[2]=r("Attached ")),a(p,{date:n.attachedAt},null,8,["date"]),a(e.MIconButton,{permission:"api.players.auth",variant:"danger","aria-label":"Remove this authentication method.",onClick:c=>e.onRemoveAuthClick(n)},{default:o(()=>[a(s,{icon:"trash-alt"})],void 0,!0),_:2},1032,["onClick"])])]),"bottom-left":o(()=>[r(m(n.id),1)]),default:o(()=>[n.type==="device"?(l(),u("span",ee,[a(s,{icon:"key"}),t[1]||(t[1]=r(" Client token"))])):(l(),u("span",te,[a(s,{icon:"user-tag"}),r(" "+m(n.displayString),1)]))],void 0,!0),_:2},1024)]),default:o(()=>[n.devices&&n.devices.length>0?(l(),u("div",ae,[t[3]||(t[3]=f("div",{class:"tw-mb-2 tw-font-semibold"},"Known Devices",-1)),a(e.MList,{showBorder:"",striped:""},{default:o(()=>[(l(!0),u(J,null,Q(n.devices,c=>(l(),F(e.MListItem,{key:c.id,condensed:""},{"top-right":o(()=>[f("span",ne,m(c.id),1)]),default:o(()=>[r(m(c.deviceModel),1)],void 0,!0),_:2},1024))),128))],void 0,!0),_:2},1024)])):(l(),u("div",re,"No devices recorded for this login method."))],void 0,!0),_:2},1024),a(e.MActionModal,{ref:"removeAuthModal",title:"Remove Authentication Method",action:e.removeAuth,onHidden:t[0]||(t[0]=c=>e.authToRemove=null)},{default:o(()=>[f("p",se,[t[4]||(t[4]=r("You are about to remove ")),e.authToRemove?(l(),F(e.MBadge,{key:0},{default:o(()=>[r(m(e.authToRemove.displayString),1)],void 0,!0),_:1})):A("",!0),t[5]||(t[5]=r(" from ")),a(e.MBadge,null,{default:o(()=>[r(m(e.playerData.model.playerName),1)],void 0,!0),_:1}),t[6]||(t[6]=r(". They will not be able to login to their account using this method."))]),e.allAuths.length===1?(l(),u("p",ie,"Note: Removing this last auth method means the account will be orphaned!")):A("",!0),a(v,{name:e.playerData.model.playerName||"n/a"},null,8,["name"])],void 0,!0),_:1},512)]),_:1},8,["itemList","emptyMessage"])])):A("",!0)}const ge=U(Z,[["render",le],["__file","PlayerLoginMethodsCard.vue"]]);export{ge as default};
